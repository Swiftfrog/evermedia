// Services/MediaInfoService.cs
using MediaBrowser.Controller;              // IServerApplicationHost
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;      // ILibraryManager IMediaSourceManager
using MediaBrowser.Controller.Persistence;  // IItemRepository
using MediaBrowser.Controller.Providers;    // IProviderManager
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Dto;               // MediaSourceInfo
using MediaBrowser.Model.IO;                // IFileSystem
using MediaBrowser.Model.Logging;           // ILogger, ILogManager
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;     // IJsonSerializer
using System.IO; // For Path operations
using System.Linq; // For OfType
using System.Threading.Tasks; // For async/await
using System.Collections.Generic; // For List

// --- 仍然需要导入 PluginConfiguration 的命名空间 ---
using EverMedia.Configuration;

namespace EverMedia.Services; // 使用命名空间组织代码

/// <summary>
/// 核心服务：负责 .strm 文件 MediaInfo 的备份与恢复逻辑。
/// </summary>
public class MediaInfoService
{
    // --- 依赖注入的私有字段 ---
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IItemRepository _itemRepository;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IMediaSourceManager _mediaSourceManager; // ✅ 添加 IMediaSourceManager 依赖

    // --- 构造函数：接收 Emby 框架注入的依赖项 ---
    public MediaInfoService(
        ILogManager logManager,           // 请求日志管理器工厂
        ILibraryManager libraryManager,   // 用于管理媒体库项目
        IItemRepository itemRepository,   // 用于直接操作数据库中的项目数据（如保存媒体流）
        IProviderManager providerManager, // 用于触发元数据刷新等
        IFileSystem fileSystem,           // 用于文件系统操作
        IJsonSerializer jsonSerializer,   // 用于序列化/反序列化 JSON
        IServerApplicationHost applicationHost, // 用于获取插件配置
        IMediaSourceManager mediaSourceManager // ✅ 注入 IMediaSourceManager
    )
    {
        // ✅ 使用 logManager 为这个服务类创建一个 logger 实例，日志前缀将是 "MediaInfoService"
        _logger = logManager.GetLogger(GetType().Name);
        _libraryManager = libraryManager;
        _itemRepository = itemRepository;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        _applicationHost = applicationHost;
        _mediaSourceManager = mediaSourceManager; // ✅ 保存 IMediaSourceManager
    }

    // --- 核心方法：备份 MediaInfo ---
    public async Task<bool> BackupAsync(BaseItem item)
    {
        _logger.Info($"[MediaInfoService] Starting BackupAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        // ✅ 在方法内部获取当前配置
        var config = GetConfiguration();
        if (config == null)
        {
            _logger.Error("[MediaInfoService] Failed to get plugin configuration for BackupAsync.");
            return false; // 配置获取失败，返回 false
        }

        try
        {
            // 1. 获取项目的 MediaSourceInfo (使用 _mediaSourceManager)
            // 需要获取 LibraryOptions
            var libraryOptions = _libraryManager.GetLibraryOptions(item);

            // ✅ 修正：检查 GetLibraryOptions 是否返回 null
            if (libraryOptions == null)
            {
                _logger.Error($"[MediaInfoService] Failed to get LibraryOptions for item: {item.Path ?? item.Name}. Cannot proceed with backup.");
                return false; // 没有库选项，无法进行后续操作
            }

            // 调用 GetStaticMediaSources (8 参数版本)
            // 参数: item, enableAlternateMediaSources, enablePathSubstitution, fillChapters, collectionFolders, libraryOptions, deviceProfile, user
            var mediaSources = _mediaSourceManager.GetStaticMediaSources(
                item,
                false, // enableAlternateMediaSources - 通常为 false 用于当前项目
                false, // enablePathSubstitution - 为 false
                true,  // fillChapters - 设为 true 以获取章节信息
                Array.Empty<BaseItem>(), // collectionFolders - 传空数组
                libraryOptions, // 从项目获取的库选项
                null,  // deviceProfile - 传 null
                null   // user - 传 null
            );

            if (mediaSources == null || !mediaSources.Any())
            {
                _logger.Info($"[MediaInfoService] No MediaSources found for item: {item.Path ?? item.Name}. Skipping backup.");
                return false; // 没有找到媒体源，无法备份
            }

            // 2. 获取章节信息 (如果 fillChapters 在 GetStaticMediaSources 中设为 true，章节信息已在 mediaSources 中)
            // 但我们也可以单独从 _itemRepository 获取
            var chapters = _itemRepository.GetChapters(item);

            // ✅ 修正：检查 GetChapters 是否返回 null
            if (chapters == null)
            {
                 _logger.Warn($"[MediaInfoService] GetChapters returned null for item: {item.Path ?? item.Name}. Using empty list.");
                 // 将 chapters 设置为空列表，以便后续代码可以安全地使用 .ToList()
                 chapters = new List<ChapterInfo>();
            }

            // 3. 创建 MediaSourceWithChapters 对象列表
            var mediaSourcesWithChapters = mediaSources.Select(ms => new MediaSourceWithChapters
            {
                MediaSourceInfo = ms, // ms 可能为 null
                Chapters = chapters.ToList() // 每个 MediaSourceInfo 都关联相同的章节列表 (或者根据需要进行筛选)
            }).ToList();

            // 4. 数据净化 (关键步骤) - 修正：过滤掉 MediaSourceInfo 为 null 的项，或在循环内检查 null
            // 方案一：在净化前过滤掉 null 项
            var validSourcesWithChapters = mediaSourcesWithChapters.Where(swc => swc.MediaSourceInfo != null).ToList();
            if (!validSourcesWithChapters.Any())
            {
                _logger.Warn($"[MediaInfoService] All MediaSourceInfo objects were null for item: {item.Path ?? item.Name}. Skipping backup.");
                return false;
            }

            foreach (var sourceWithChapters in validSourcesWithChapters)
            {
                // 此时 sourceWithChapters.MediaSourceInfo 已确认不为 null
                var msInfo = sourceWithChapters.MediaSourceInfo!;
                // 清除临时/会话相关字段
                msInfo.Id = null;
                msInfo.ItemId = null;
                // msInfo.Path 对于 *外挂* 字幕流需要特殊处理，其他流的 Path 也应清空
                msInfo.Path = null; // 清空主 Path

                // 处理外挂字幕流的路径
                // msInfo.MediaStreams 也可能为 null，需要检查
                if (msInfo.MediaStreams != null)
                {
                    foreach (var stream in msInfo.MediaStreams.Where(s => s.IsExternal && s.Type == MediaStreamType.Subtitle && s.Path != null))
                    {
                        // 只保存文件名，而不是完整路径
                        stream.Path = Path.GetFileName(stream.Path);
                    }
                }

                // 清理章节信息中的临时字段 (如果有的话)
                foreach (var chapter in sourceWithChapters.Chapters)
                {
                    chapter.ImageTag = null; // 清除可能的图片标签
                }
            }

            // 5. 生成 .medinfo 文件路径
            string medInfoPath = GetMedInfoPath(item);

            // 6. 创建 .medinfo 文件的父目录（如果不存在）
            var parentDir = Path.GetDirectoryName(medInfoPath);
            if (!string.IsNullOrEmpty(parentDir) && !_fileSystem.DirectoryExists(parentDir))
            {
                _fileSystem.CreateDirectory(parentDir);
            }


            // 7. 添加版本信息到 DTO
            // ✅ 修正：获取插件版本时，通过 _applicationHost 获取，更安全
            var plugin = _applicationHost.Plugins.OfType<Plugin>().FirstOrDefault();
            var pluginVersionString = plugin?.Version.ToString() ?? "Unknown";

            var backupData = new
            {
                EmbyVersion = _applicationHost.ApplicationVersion.ToString(),
                PluginVersion = pluginVersionString, // 使用安全获取的版本
                Data = validSourcesWithChapters // 使用过滤后的列表
            };

            // 8. 序列化到 .medinfo 文件 (修正：使用 SerializeToFile 并包装在 Task.Run 中)
            // IJsonSerializer 没有 SerializeToFileAsync，所以使用同步方法并用 Task.Run 避免阻塞
            await Task.Run(() => _jsonSerializer.SerializeToFile(backupData, medInfoPath));

            _logger.Info($"[MediaInfoService] Backup completed for item: {item.Path ?? item.Name}. File written: {medInfoPath}");
            return true;


        }
        catch (Exception ex)
        {
            _logger.Error($"[MediaInfoService] Error during BackupAsync for item {item.Path ?? item.Name}: {ex.Message}");
            _logger.Debug(ex.StackTrace); // 记录详细堆栈
            return false; // 发生错误，返回 false
        }
    }


    // --- 核心方法：恢复 MediaInfo ---
    public async Task<bool> RestoreAsync(BaseItem item)
    {
        _logger.Info($"[MediaInfoService] Starting RestoreAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        // ✅ 在方法内部获取当前配置
        var config = GetConfiguration();
        if (config == null)
        {
            _logger.Error("[MediaInfoService] Failed to get plugin configuration for RestoreAsync.");
            return false; // 配置获取失败，返回 false
        }

        // TODO: 实现恢复逻辑
        // 1. 查找对应的 .medinfo 文件 (使用 config.BackupMode)
        // 2. 反序列化 JSON
        // 3. 更新 BaseItem 属性
        // 4. 调用 _itemRepository.SaveMediaStreams
        // 5. 调用 _libraryManager.UpdateItem
        // 6. 记录成功或失败

        _logger.Info($"[MediaInfoService] RestoreAsync completed for item: {item.Path ?? item.Name}. Config used: BackupMode={config.BackupMode}. Result: Not Implemented Yet.");
        return false; // 暂时返回 false
    }

    // --- 辅助方法：获取插件配置 ---
    private PluginConfiguration? GetConfiguration()
    {
        // ✅ 通过 IServerApplicationHost 获取插件实例，然后获取配置
        var plugin = _applicationHost.Plugins.OfType<Plugin>().FirstOrDefault();
        return plugin?.Configuration;
    }

    // --- 辅助方法：生成 .medinfo 文件路径 ---
    private string GetMedInfoPath(BaseItem item)
    {
        // ✅ 修正：检查 item.Path 是否为 null
        if (string.IsNullOrEmpty(item.Path))
        {
            _logger.Error($"[MediaInfoService] Item path is null or empty for item ID: {item.Id}. Cannot generate MedInfo path.");
            // 返回一个默认路径或抛出异常，取决于你的处理策略
            // 这里我们返回一个基于 ID 的默认路径
            return Path.Combine(item.ContainingFolderPath, item.Id.ToString() + ".medinfo");
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        string medInfoFileName = fileNameWithoutExtension + ".medinfo";

        // ✅ 在方法内部获取当前配置
        var config = GetConfiguration();
        if (config == null)
        {
             _logger.Warn("[MediaInfoService] Failed to get plugin configuration for GetMedInfoPath, using default SideBySide mode.");
             // 如果配置获取失败，返回 SideBySide 模式下的路径作为默认值
             return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }

        // 使用配置
        if (config.BackupMode == "Centralized" && !string.IsNullOrEmpty(config.CentralizedRootPath))
        {
            // 如果是中心化模式且路径有效，则构建中心化路径
            // 注意：GetRelativePath 可能需要处理不同的根目录情况
            string itemDir = Path.GetDirectoryName(item.Path) ?? item.ContainingFolderPath;
            string relativePath = Path.GetRelativePath(item.ContainingFolderPath, itemDir);
            // GetRelativePath 可能返回 "." 或 ".." 或包含 ".." 的路径，需要处理
            if (relativePath == ".")
            {
                relativePath = string.Empty; // 表示与 ContainingFolderPath 相同
            }
            else if (relativePath.StartsWith(".."))
            {
                // 如果相对路径向上跳出了 ContainingFolderPath，可能需要警告或特殊处理
                _logger.Warn($"[MediaInfoService] Relative path calculation for centralized storage resulted in '{relativePath}' for item '{item.Path}'. Using SideBySide mode for this item.");
                 return Path.Combine(item.ContainingFolderPath, medInfoFileName);
            }
            return Path.Combine(config.CentralizedRootPath, relativePath, medInfoFileName);
        }
        else
        {
            // 默认：SideBySide 模式，.medinfo 文件与 .strm 文件同目录
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }
    }

    // --- 内部类：用于序列化/反序列化的数据结构 (借鉴 StrmAssistant) ---
    internal class MediaSourceWithChapters
    {
        public MediaSourceInfo? MediaSourceInfo { get; set; } // 可能为 null
        public List<ChapterInfo> Chapters { get; set; } = new List<ChapterInfo>();
        // 可以根据需要添加其他字段，如 ZeroFingerprintConfidence, EmbeddedImage 等
        public bool? ZeroFingerprintConfidence { get; set; }
        public string? EmbeddedImage { get; set; }
    }
}
