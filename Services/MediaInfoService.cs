// Services/MediaInfoService.cs
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System; // For Exception
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EverMedia.Configuration;

namespace EverMedia.Services;

/// <summary>
/// 核心服务：负责.strm 文件 MediaInfo 的备份与恢复逻辑。
/// </summary>
public class MediaInfoService
{
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IItemRepository _itemRepository;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IServerApplicationHost _applicationHost;
    private readonly IMediaSourceManager _mediaSourceManager;

    public MediaInfoService(
        ILogManager logManager,
        ILibraryManager libraryManager,
        IItemRepository itemRepository,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        IJsonSerializer jsonSerializer,
        IServerApplicationHost applicationHost,
        IMediaSourceManager mediaSourceManager
    )
    {
        // ✅ 最佳实践: 使用泛型 CreateLogger<T> 以获得更精确的日志分类
        _logger = logManager.CreateLogger<MediaInfoService>();
        _libraryManager = libraryManager;
        _itemRepository = itemRepository;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        _applicationHost = applicationHost;
        _mediaSourceManager = mediaSourceManager;
    }

    public async Task<bool> BackupAsync(BaseItem item)
    {
        // ✅ 最佳实践: 使用结构化日志记录
        _logger.LogInformation("Starting BackupAsync for item: {ItemPath} (ID: {ItemId})", item.Path?? item.Name, item.Id);

        var config = GetConfiguration();
        if (config == null)
        {
            _logger.LogError("Failed to get plugin configuration for BackupAsync.");
            return false;
        }

        try
        {
            var libraryOptions = _libraryManager.GetLibraryOptions(item);
            if (libraryOptions == null)
            {
                _logger.LogError("Failed to get LibraryOptions for item: {ItemPath}. Cannot proceed with backup.", item.Path?? item.Name);
                return false;
            }

            var mediaSources = _mediaSourceManager.GetStaticMediaSources(item, false, false, true, Array.Empty<BaseItem>(), libraryOptions, null, null);
            if (mediaSources == null ||!mediaSources.Any())
            {
                _logger.LogInformation("No MediaSources found for item: {ItemPath}. Skipping backup.", item.Path?? item.Name);
                return false;
            }

            var chapters = _itemRepository.GetChapters(item)?? new List<ChapterInfo>();

            var mediaSourcesWithChapters = mediaSources.Select(ms => new MediaSourceWithChapters
            {
                MediaSourceInfo = ms,
                Chapters = chapters.ToList()
            }).ToList();

            foreach (var sourceWithChapters in mediaSourcesWithChapters)
            {
                var msInfo = sourceWithChapters.MediaSourceInfo;

                // ✅ 解决方案 1: 解决 CS8602 警告
                // 在解引用之前，必须检查 msInfo 是否为 null。
                if (msInfo == null)
                {
                    _logger.LogWarning("A null MediaSourceInfo was found in the media sources list for item {ItemId}. Skipping this entry.", item.Id);
                    continue; // 跳过这个 null 的条目，继续处理下一个
                }

                // --- 从这里开始，编译器知道 msInfo 不是 null ---
                msInfo.Id = null;
                msInfo.ItemId = null;
                msInfo.Path = null;

                if (msInfo.MediaStreams!= null)
                {
                    foreach (var stream in msInfo.MediaStreams.Where(s => s.IsExternal && s.Type == MediaStreamType.Subtitle && s.Path!= null))
                    {
                        stream.Path = Path.GetFileName(stream.Path);
                    }
                }

                foreach (var chapter in sourceWithChapters.Chapters)
                {
                    chapter.ImageTag = null;
                }
            }

            string medInfoPath = GetMedInfoPath(item);
            if (string.IsNullOrEmpty(medInfoPath))
            {
                _logger.LogError("Failed to generate a valid.medinfo path for item: {ItemPath}. Aborting backup.", item.Path?? item.Name);
                return false;
            }

            var parentDir = Path.GetDirectoryName(medInfoPath);
            if (!string.IsNullOrEmpty(parentDir) &&!_fileSystem.DirectoryExists(parentDir))
            {
                _fileSystem.CreateDirectory(parentDir);
            }

            // ✅ 解决方案 3: 安全地获取插件实例和版本，而不是依赖静态实例
            var plugin = _applicationHost.Plugins.OfType<Plugin>().FirstOrDefault();
            if (plugin == null)
            {
                _logger.LogError("Could not find the running plugin instance. Cannot complete backup.");
                return false;
            }

            var backupData = new
            {
                EmbyVersion = _applicationHost.ApplicationVersion.ToString(),
                PluginVersion = plugin.Version.ToString(),
                Data = mediaSourcesWithChapters
            };

            await Task.Run(() => _jsonSerializer.SerializeToFile(backupData, medInfoPath));

            _logger.LogInformation("Backup completed for item: {ItemPath}. File written: {MedInfoPath}", item.Path?? item.Name, medInfoPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BackupAsync for item {ItemPath}", item.Path?? item.Name);
            return false;
        }
    }

    public async Task<bool> RestoreAsync(BaseItem item)
    {
        _logger.LogInformation("Starting RestoreAsync for item: {ItemPath} (ID: {ItemId})", item.Path?? item.Name, item.Id);

        var config = GetConfiguration();
        if (config == null)
        {
            _logger.LogError("Failed to get plugin configuration for RestoreAsync.");
            return false;
        }

        // TODO: 实现恢复逻辑
        // 1. 查找对应的.medinfo 文件 (使用 GetMedInfoPath)
        // 2. 反序列化 JSON
        // 3. 更新 BaseItem 属性
        // 4. 调用 _itemRepository.SaveMediaStreams
        // 5. 调用 _libraryManager.UpdateItem
        // 6. 记录成功或失败

        _logger.LogInformation("RestoreAsync completed for item: {ItemPath}. Config used: BackupMode={BackupMode}. Result: Not Implemented Yet.", item.Path?? item.Name, config.BackupMode);
        
        // 确保异步方法有 await 调用，否则会有编译器警告
        await Task.CompletedTask; 
        return false;
    }

    private PluginConfiguration? GetConfiguration()
    {
        var plugin = _applicationHost.Plugins.OfType<Plugin>().FirstOrDefault();
        return plugin?.Configuration;
    }

    private string GetMedInfoPath(BaseItem item)
    {
        // ✅ 解决方案 2: 在使用 item.Path 之前，必须检查它是否为 null
        if (string.IsNullOrEmpty(item.Path))
        {
            _logger.LogError("Item path is null or empty for item '{ItemName}' (ID: {ItemId}). Cannot generate.medinfo path.", item.Name, item.Id);
            return string.Empty; // 返回空字符串表示失败
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        string medInfoFileName = fileNameWithoutExtension + ".medinfo";

        var config = GetConfiguration();
        if (config == null)
        {
            _logger.LogWarning("Failed to get plugin configuration for GetMedInfoPath, using default SideBySide mode.");
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }

        if (config.BackupMode == "Centralized" &&!string.IsNullOrEmpty(config.CentralizedRootPath))
        {
            // 你的中心化逻辑...
            return Path.Combine(config.CentralizedRootPath, medInfoFileName); // 简化示例
        }
        else
        {
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }
    }

    internal class MediaSourceWithChapters
    {
        public MediaSourceInfo? MediaSourceInfo { get; set; }
        public List<ChapterInfo> Chapters { get; set; } = new List<ChapterInfo>();
        public bool? ZeroFingerprintConfidence { get; set; }
        public string? EmbeddedImage { get; set; }
    }
}
