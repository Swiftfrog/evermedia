// Services/MediaInfoService.cs
using MediaBrowser.Controller;              // IServerApplicationHost
using MediaBrowser.Controller.Entities;     // BaseItem
using MediaBrowser.Controller.Library;      // ILibraryManager
using MediaBrowser.Controller.Persistence;  // IItemRepository
using MediaBrowser.Controller.Providers;    // IProviderManager
using MediaBrowser.Controller.Plugins;      // IServerApplicationHost
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;                // IFileSystem
using MediaBrowser.Model.Logging;           // ILogger
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;     // IJsonSerializer
using System.IO; // For Path operations
using System.Linq; // For OfType
using System.Threading.Tasks; // For async/await

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
    // ✅ 添加对 IServerApplicationHost 的引用
    private readonly IServerApplicationHost _applicationHost;

    // --- 构造函数：接收 Emby 框架注入的依赖项 ---
    public MediaInfoService(
        ILogger logger,                   // 用于记录日志
        ILibraryManager libraryManager,   // 用于管理媒体库项目
        IItemRepository itemRepository,   // 用于直接操作数据库中的项目数据（如保存媒体流）
        IProviderManager providerManager, // 用于触发元数据刷新等
        IFileSystem fileSystem,           // 用于文件系统操作
        IJsonSerializer jsonSerializer,   // 用于序列化/反序列化 JSON
        // ✅ 注入 IServerApplicationHost
        IServerApplicationHost applicationHost
    )
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _itemRepository = itemRepository;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        // ✅ 保存 IServerApplicationHost
        _applicationHost = applicationHost;
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

        // TODO: 实现备份逻辑
        // 1. 获取项目的 MediaSourceInfo (使用 _libraryManager 或 _mediaSourceManager)
        // 2. 清理数据 (移除 Id, Path 等临时字段)
        // 3. 序列化到 .medinfo 文件 (使用 config.BackupMode)
        // 4. 记录成功或失败

        _logger.Info($"[MediaInfoService] BackupAsync completed for item: {item.Path ?? item.Name}. Config used: BackupMode={config.BackupMode}. Result: Not Implemented Yet.");
        return false; // 暂时返回 false
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
        return plugin?.Configuration; // 如果插件实例或配置为 null，返回 null
    }

    // --- 辅助方法：生成 .medinfo 文件路径 ---
    private string GetMedInfoPath(BaseItem item)
    {
        // ✅ 在方法内部获取当前配置
        var config = GetConfiguration();
        if (config == null)
        {
             _logger.Warn("[MediaInfoService] Failed to get plugin configuration for GetMedInfoPath, using default SideBySide mode.");
             // 如果配置获取失败，返回 SideBySide 模式下的路径作为默认值
             string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
             string medInfoFileName = fileNameWithoutExtension + ".medinfo";
             return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        string medInfoFileName = fileNameWithoutExtension + ".medinfo";

        // 使用配置
        if (config.BackupMode == "Centralized" && !string.IsNullOrEmpty(config.CentralizedRootPath))
        {
            // 如果是中心化模式且路径有效，则构建中心化路径
            string relativePath = Path.GetRelativePath(item.ContainingFolderPath, Path.GetDirectoryName(item.Path) ?? string.Empty);
            return Path.Combine(config.CentralizedRootPath, relativePath, medInfoFileName);
        }
        else
        {
            // 默认：SideBySide 模式，.medinfo 文件与 .strm 文件同目录
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }
    }
}
