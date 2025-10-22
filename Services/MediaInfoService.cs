// Services/MediaInfoService.cs
using MediaBrowser.Controller.Entities;     // BaseItem
using MediaBrowser.Controller.Library;      // ILibraryManager
using MediaBrowser.Controller.Persistence;  // IItemRepository
using MediaBrowser.Controller.Plugins;      // ConfigurationPageType PluginConfiguration
using MediaBrowser.Controller.Providers;    // IProviderManager
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;                // IFileSystem
using MediaBrowser.Model.Logging;           // ILogger
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;     // IJsonSerializer
using System.IO; // For Path operations
using System.Threading.Tasks;               // For async/await
using EverMedia.Configuration;              //导入 PluginConfiguration 所在的命名空间

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

    // --- 服务配置 ---
    // 从插件主类获取配置
    private readonly PluginConfiguration _configuration;

    // --- 构造函数：接收 Emby 框架注入的依赖项 ---
    public MediaInfoService(
        ILogger logger,                   // 用于记录日志
        ILibraryManager libraryManager,   // 用于管理媒体库项目
        IItemRepository itemRepository,   // 用于直接操作数据库中的项目数据（如保存媒体流）
        IProviderManager providerManager, // 用于触发元数据刷新等
        IFileSystem fileSystem,           // 用于文件系统操作
        IJsonSerializer jsonSerializer    // 用于序列化/反序列化 JSON
    )
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _itemRepository = itemRepository;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;

        // 从插件实例获取当前配置
        _configuration = Plugin.Instance.Configuration;
    }

    // --- 核心方法：备份 MediaInfo ---
    public async Task<bool> BackupAsync(BaseItem item)
    {
        _logger.Info($"[MediaInfoService] Starting BackupAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        // TODO: 实现备份逻辑
        // 1. 获取项目的 MediaSourceInfo (使用 _libraryManager 或 _mediaSourceManager)
        // 2. 清理数据 (移除 Id, Path 等临时字段)
        // 3. 序列化到 .medinfo 文件
        // 4. 记录成功或失败

        _logger.Info($"[MediaInfoService] BackupAsync completed for item: {item.Path ?? item.Name}. Result: Not Implemented Yet.");
        return false; // 暂时返回 false
    }

    // --- 核心方法：恢复 MediaInfo ---
    public async Task<bool> RestoreAsync(BaseItem item)
    {
        _logger.Info($"[MediaInfoService] Starting RestoreAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        // TODO: 实现恢复逻辑
        // 1. 查找对应的 .medinfo 文件
        // 2. 反序列化 JSON
        // 3. 更新 BaseItem 属性
        // 4. 调用 _itemRepository.SaveMediaStreams
        // 5. 调用 _libraryManager.UpdateItem
        // 6. 记录成功或失败

        _logger.Info($"[MediaInfoService] RestoreAsync completed for item: {item.Path ?? item.Name}. Result: Not Implemented Yet.");
        return false; // 暂时返回 false
    }

    // --- 辅助方法：生成 .medinfo 文件路径 ---
    private string GetMedInfoPath(BaseItem item)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        string medInfoFileName = fileNameWithoutExtension + ".medinfo";

        if (_configuration.BackupMode == "Centralized" && !string.IsNullOrEmpty(_configuration.CentralizedRootPath))
        {
            // 如果是中心化模式且路径有效，则构建中心化路径
            string relativePath = Path.GetRelativePath(item.ContainingFolderPath, Path.GetDirectoryName(item.Path) ?? string.Empty);
            return Path.Combine(_configuration.CentralizedRootPath, relativePath, medInfoFileName);
        }
        else
        {
            // 默认：SideBySide 模式，.medinfo 文件与 .strm 文件同目录
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }
    }
}
