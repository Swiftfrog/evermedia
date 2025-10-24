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
    // private readonly IMediaSourceManager _mediaSourceManager; // ✅ 不再需要注入 IMediaSourceManager

    // --- 构造函数：接收 Emby 框架注入的依赖项 ---
    public MediaInfoService(
        ILogManager logManager,           // 请求日志管理器工厂
        ILibraryManager libraryManager,   // 用于管理媒体库项目
        IItemRepository itemRepository,   // 用于直接操作数据库中的项目数据（如保存媒体流）
        IProviderManager providerManager, // 用于触发元数据刷新等
        IFileSystem fileSystem,           // 用于文件系统操作
        IJsonSerializer jsonSerializer,   // 用于序列化/反序列化 JSON
        IServerApplicationHost applicationHost // 用于获取插件配置
        // IMediaSourceManager mediaSourceManager // ✅ 不再注入 IMediaSourceManager
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
        // _mediaSourceManager = mediaSourceManager; // ✅ 不再保存 IMediaSourceManager
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
            // 1. 获取项目的 LibraryOptions
            var libraryOptions = _libraryManager.GetLibraryOptions(item);
            if (libraryOptions == null)
            {
                _logger.Error($"[MediaInfoService] Failed to get LibraryOptions for item: {item.Path ?? item.Name}. Cannot proceed with backup.");
                return false; // 没有库选项，无法进行后续操作
            }

            // 1. 获取项目的 MediaSourceInfo (使用 item.GetMediaSources - 选择合适的重载)
            // 使用参数较少的重载，适用于 CoverArt，但也适用于获取当前已加载的信息
            // 参数: enableAlternateMediaSources, enablePathSubstitution, libraryOptions
            var mediaSources = item.GetMediaSources(false, false, libraryOptions); // ✅ 修正：使用带参数的 GetMediaSources

            if (mediaSources == null || !mediaSources.Any())
            {
                _logger.Info($"[MediaInfoService] No MediaSources found via GetMediaSources for item: {item.Path ?? item.Name}. Skipping backup.");
                return false; // 没有找到媒体源，无法备份
            }

            // 2. 获取章节信息 (章节信息通常已包含在 GetMediaSources 返回的 MediaSourceInfo 对象中)
            // 我们可以再次从 item 获取，但 GetMediaSources 通常已处理好
            // var chapters = _itemRepository.GetChapters(item); // 如果需要单独获取，可以保留
            // 但 StrmAssistant 的方式是将 item.GetMediaSources() 返回的 MediaSourceInfo 对象直接用作 MediaSourceWithChapters.MediaSourceInfo
            // 并且 StrmAssistant 在 Backup 时也会获取章节，然后赋值给 MediaSourceWithChapters.Chapters

            // StrmAssistant 风格：获取章节
            var chapters = _itemRepository.GetChapters(item);

            // 3. 创建 MediaSourceWithChapters 对象列表
            // 这里我们假设 item.GetMediaSources() 返回的每个 MediaSourceInfo 都可能关联相同的章节列表
            // 或者，如果每个 MediaSourceInfo 都有自己的章节，需要更复杂的映射
            // StrmAssistant 的代码片段显示它将所有 MediaSourceInfo 都关联到同一个章节列表
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
        // ✅ 修正日志前缀为 "EverMedia" + 类名
        _logger.Info($"[EverMedia:MediaInfoService] Starting RestoreAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        var config = GetConfiguration();
        if (config == null)
        {
            _logger.Error("[EverMedia:MediaInfoService] Failed to get plugin configuration for RestoreAsync.");
            return false;
        }

        try
        {
            // 1. 查找对应的 .medinfo 文件
            string medInfoPath = GetMedInfoPath(item);
            _logger.Debug($"[EverMedia:MediaInfoService] Looking for medinfo file: {medInfoPath}");

            if (!_fileSystem.FileExists(medInfoPath))
            {
                _logger.Info($"[EverMedia:MediaInfoService] No medinfo file found for item: {item.Path ?? item.Name}. Path checked: {medInfoPath}");
                return false; // 文件不存在，无法恢复
            }

            // 2. 读取并反序列化 JSON
            // 首先反序列化为 object 以检查结构
            object? backupDataObject = null;
            try
            {
                backupDataObject = await _jsonSerializer.DeserializeFromFileAsync<object>(medInfoPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"[EverMedia:MediaInfoService] Error deserializing medinfo file {medInfoPath}: {ex.Message}");
                return false;
            }

            if (backupDataObject == null)
            {
                _logger.Warn($"[EverMedia:MediaInfoService] Deserialized medinfo file {medInfoPath} is null.");
                return false;
            }

            // 尝试将其视为我们备份时写入的结构: { EmbyVersion, PluginVersion, Data: [MediaSourceWithChapters...] }
            // 需要动态处理或创建一个临时的 DTO 来接收
            // 为了类型安全，我们创建一个临时的 DTO 来反序列化
            // var backupDto = new { EmbyVersion = "", PluginVersion = "", Data = Array.Empty<MediaSourceWithChapters>() };
            BackupDto? backupDto = null;
            try
            {
                backupDto = _jsonSerializer.DeserializeFromFile<BackupDto>(medInfoPath);
            }
            catch (Exception ex)
            {
                 _logger.Error($"[EverMedia:MediaInfoService] Error deserializing medinfo file {medInfoPath} into BackupDto: {ex.Message}");
                 _logger.Debug(ex.StackTrace); // 记录详细堆栈
                 return false;
            }

            if (backupDto == null || backupDto.Data == null || !backupDto.Data.Any())
            {
                _logger.Warn($"[EverMedia:MediaInfoService] No data found in medinfo file {medInfoPath}.");
                return false;
            }


            // 3. 版本检查 (可选)
            _logger.Debug($"[EverMedia:MediaInfoService] Restoring from EmbyVersion: {backupDto.EmbyVersion ?? "Unknown"}, PluginVersion: {backupDto.PluginVersion ?? "Unknown"}");
            // TODO: 在这里可以添加版本兼容性检查逻辑

            // 4. 选择要恢复的数据 (通常取第一个)
            var sourceToRestore = backupDto.Data.First(); // 简单起见，恢复第一个 MediaSourceWithChapters
            var mediaSourceInfo = sourceToRestore.MediaSourceInfo;
            var chaptersToRestore = sourceToRestore.Chapters ?? new List<ChapterInfo>();

            if (mediaSourceInfo == null)
            {
                _logger.Warn($"[EverMedia:MediaInfoService] MediaSourceInfo in medinfo file {medInfoPath} is null.");
                return false;
            }

            // 5. 数据恢复
            // 5a. 更新 BaseItem 属性
            // 注意：需要获取可变的 BaseItem 实例（通常是传入的 item，但确保它是最新的）
            // item 参数通常是可以直接修改的
            item.Size = mediaSourceInfo.Size.GetValueOrDefault();
            item.RunTimeTicks = mediaSourceInfo.RunTimeTicks;
            item.Container = mediaSourceInfo.Container;
            item.TotalBitrate = mediaSourceInfo.Bitrate.GetValueOrDefault();

            // 更新视频流属性 (如果存在)
            var videoStream = mediaSourceInfo.MediaStreams
                .Where(s => s.Type == MediaStreamType.Video && s.Width.HasValue && s.Height.HasValue)
                .OrderByDescending(s => (long)s.Width!.Value * s.Height!.Value)
                .FirstOrDefault();

            if (videoStream != null)
            {
                item.Width = videoStream.Width.GetValueOrDefault();
                item.Height = videoStream.Height.GetValueOrDefault();
            }

            // 5b. 恢复媒体流 (使用 IItemRepository)
            // 注意：MediaStreams 需要与 item.Id 关联
            // GetStaticMediaSources 返回的 MediaSourceInfo.MediaStreams 可能需要调整 Id

            // ✅ 修正 2: 使用 item.InternalId (long) 而不是 item.Id (Guid)
            var streamsToSave = mediaSourceInfo.MediaStreams?.ToList() ?? new List<MediaStream>();
            // 确保流的 Id 与 item.Id 关联 (通常在 SaveMediaStreams 时由框架处理，但检查一下)
            // MediaStream 对象本身通常不需要手动设置 ItemId，SaveMediaStreams 会处理
            // 但 Path 字段，对于外挂字幕，需要在恢复时重建完整路径
            foreach (var stream in streamsToSave.Where(s => s.IsExternal && s.Type == MediaStreamType.Subtitle && s.Path != null))
            {
                // 重建完整路径：媒体文件所在目录 + 保存的相对文件名
                stream.Path = Path.Combine(item.ContainingFolderPath, stream.Path);
            }

            // 调用 SaveMediaStreams
            _logger.Debug($"[EverMedia:MediaInfoService] Saving {streamsToSave.Count} media streams for item: {item.Path ?? item.Name}");
            // ✅ 修正 2: 使用 item.InternalId
            _itemRepository.SaveMediaStreams(item.InternalId, streamsToSave, CancellationToken.None); // 注意：CancellationToken.None

            // 5c. 恢复章节 (使用 IItemRepository)
            // ✅ 修正 2: 使用 item.InternalId (long) 而不是 item.Id (Guid)
            // 需要重建章节图片路径（如果之前保存了图片）
            // foreach (var chapter in chaptersToRestore.Where(c => !string.IsNullOrEmpty(c.ImagePath)))
            // {
            //     chapter.ImagePath = Path.Combine(item.ContainingFolderPath, Path.GetFileName(chapter.ImagePath));
            // }
            // 但 StrmAssistant 似乎没有持久化图片路径，而是图片标签 (ImageTag)，并且在恢复时清空了它。
            // 我们遵循 StrmAssistant 的模式，清空 ImageTag。


            // 5c. 恢复章节 (使用 IItemRepository)
            // 需要重建章节图片路径（如果之前保存了图片）
            // foreach (var chapter in chaptersToRestore.Where(c => !string.IsNullOrEmpty(c.ImagePath)))
            // {
            //     chapter.ImagePath = Path.Combine(item.ContainingFolderPath, Path.GetFileName(chapter.ImagePath));
            // }
            // 但 StrmAssistant 似乎没有持久化图片路径，而是图片标签 (ImageTag)，并且在恢复时清空了它。
            // 我们遵循 StrmAssistant 的模式，清空 ImageTag。
            foreach (var chapter in chaptersToRestore)
            {
                chapter.ImageTag = null; // 清空图片标签，避免上下文问题
            }

            _logger.Debug($"[EverMedia:MediaInfoService] Saving {chaptersToRestore.Count} chapters for item: {item.Path ?? item.Name}");
            _itemRepository.SaveChapters(item.InternalId, true, chaptersToRestore);

            // 5d. 更新项目并通知 (使用 ILibraryManager)
            _logger.Debug($"[EverMedia:MediaInfoService] Updating item in library for: {item.Path ?? item.Name}");
            // 使用 UpdateItems 或 UpdateItem
            // 推荐使用 UpdateItems 以明确指定更新原因
            _libraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataImport, null);
            _logger.Info($"[EverMedia:MediaInfoService] Restore completed successfully for item: {item.Path ?? item.Name}. File used: {medInfoPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia:MediaInfoService] Error during RestoreAsync for item {item.Path ?? item.Name}: {ex.Message}");
            _logger.Debug(ex.StackTrace); // 记录详细堆栈
            return false;
        }
    }

    // --- 内部类：用于反序列化备份文件的 DTO ---
    private class BackupDto
    {
        public string? EmbyVersion { get; set; }
        public string? PluginVersion { get; set; }
        public MediaSourceWithChapters[] Data { get; set; } = Array.Empty<MediaSourceWithChapters>();
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
