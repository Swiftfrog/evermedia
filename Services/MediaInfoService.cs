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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EverMedia.Configuration;

namespace EverMedia.Services;

/// <summary>
/// 核心服务：负责 .strm 文件 MediaInfo 的备份与恢复逻辑。
/// </summary>
public class EverMediaService
{
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IItemRepository _itemRepository;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IServerApplicationHost _applicationHost;

    // ✅ 缓存插件实例，避免重复遍历
    private Plugin? _cachedPlugin;

    public EverMediaService(
        ILogManager logManager,
        ILibraryManager libraryManager,
        IItemRepository itemRepository,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        IJsonSerializer jsonSerializer,
        IServerApplicationHost applicationHost)
    {
        _logger = logManager.GetLogger(GetType().Name);
        _libraryManager = libraryManager;
        _itemRepository = itemRepository;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        _applicationHost = applicationHost;
    }

    // --- 缓存插件实例 ---
    private Plugin? GetPlugin()
    {
        return _cachedPlugin ??= _applicationHost.Plugins.OfType<Plugin>().FirstOrDefault();
    }

    private PluginConfiguration? GetConfiguration()
    {
        return GetPlugin()?.Configuration;
    }

    // --- 核心方法：备份 MediaInfo ---
    public async Task<bool> BackupAsync(BaseItem item)
    {
        _logger.Info($"[EverMediaService] Starting BackupAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        // 在方法内部获取当前配置
        var config = GetConfiguration();
        if (config == null)
        {
            _logger.Error("[EverMediaService] Failed to get plugin configuration for BackupAsync.");
            return false;
        }

        try
        {
            // 获取项目的 LibraryOptions
            var libraryOptions = _libraryManager.GetLibraryOptions(item);
            if (libraryOptions == null)
            {
                _logger.Error($"[EverMediaService] Failed to get LibraryOptions for item: {item.Path ?? item.Name}. Cannot proceed with backup.");
                return false;
            }

            // 1. 获取项目的 MediaSourceInfo (使用 item.GetMediaSources - 选择合适的重载)
            // 使用参数较少的重载，适用于 CoverArt，但也适用于获取当前已加载的信息
            // 参数: enableAlternateMediaSources, enablePathSubstitution, libraryOptions
            var mediaSources = item.GetMediaSources(false, false, libraryOptions); // ✅ 修正：使用带参数的 GetMediaSources

            if (mediaSources == null || !mediaSources.Any())
            {
                _logger.Info($"[EverMediaService] No MediaSources found via GetMediaSources for item: {item.Path ?? item.Name}. Skipping backup.");
                return false; // 没有找到媒体源，无法备份
            }
            
            // 获取章节
            var chapters = _itemRepository.GetChapters(item);

            var mediaSourcesWithChapters = mediaSources.Select(ms => new MediaSourceWithChapters
            {
                MediaSourceInfo = ms,
                Chapters = chapters.ToList()
            }).ToList();

            var validSourcesWithChapters = mediaSourcesWithChapters.Where(swc => swc.MediaSourceInfo != null).ToList();
            if (!validSourcesWithChapters.Any())
            {
                _logger.Warn($"[EverMediaService] All MediaSourceInfo objects were null for item: {item.Path ?? item.Name}. Skipping backup.");
                return false;
            }

            foreach (var sourceWithChapters in validSourcesWithChapters)
            {
                var msInfo = sourceWithChapters.MediaSourceInfo!;
                msInfo.Id = null;
                msInfo.ItemId = null;
                msInfo.Path = null;

                if (msInfo.MediaStreams != null)
                {
                    foreach (var stream in msInfo.MediaStreams.Where(s => s.IsExternal && s.Type == MediaStreamType.Subtitle && s.Path != null))
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
            var parentDir = Path.GetDirectoryName(medInfoPath);
            if (!string.IsNullOrEmpty(parentDir) && !_fileSystem.DirectoryExists(parentDir))
            {
                _fileSystem.CreateDirectory(parentDir);
            }

            var plugin = GetPlugin();
            var pluginVersionString = plugin?.Version.ToString() ?? "Unknown";

            var backupData = new
            {
                EmbyVersion = _applicationHost.ApplicationVersion.ToString(),
                PluginVersion = pluginVersionString,
                Data = validSourcesWithChapters
            };

            // SerializeToFile is synchronous; wrap in Task.Run to avoid blocking
            await Task.Run(() => _jsonSerializer.SerializeToFile(backupData, medInfoPath));

            _logger.Info($"[EverMediaService] Backup completed for item: {item.Path ?? item.Name}. File written: {medInfoPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMediaService] Error during BackupAsync for item {item.Path ?? item.Name}: {ex.Message}");
            _logger.Debug(ex.StackTrace);
            return false;
        }
    }

    // --- 核心方法：恢复 MediaInfo ---
    public async Task<bool> RestoreAsync(BaseItem item)
    {
        _logger.Info($"[EverMediaService] Starting RestoreAsync for item: {item.Path ?? item.Name} (ID: {item.Id})");

        var config = GetConfiguration();
        if (config == null)
        {
            _logger.Error("[EverMediaService] Failed to get plugin configuration for RestoreAsync.");
            return false;
        }

        try
        {
            string medInfoPath = GetMedInfoPath(item);
            _logger.Debug($"[EverMediaService] Looking for medinfo file: {medInfoPath}");

            if (!_fileSystem.FileExists(medInfoPath))
            {
                _logger.Info($"[EverMediaService] No medinfo file found for item: {item.Path ?? item.Name}. Path checked: {medInfoPath}");
                return false;
            }

            BackupDto? backupDto = null;
            try
            {
                backupDto = _jsonSerializer.DeserializeFromFile<BackupDto>(medInfoPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"[EverMediaService] Error deserializing medinfo file {medInfoPath} into BackupDto: {ex.Message}");
                _logger.Debug(ex.StackTrace);
                return false;
            }

            if (backupDto == null || backupDto.Data == null || !backupDto.Data.Any())
            {
                _logger.Warn($"[EverMediaService] No data found in medinfo file {medInfoPath}.");
                return false;
            }

            _logger.Debug($"[EverMediaService] Restoring from EmbyVersion: {backupDto.EmbyVersion ?? "Unknown"}, PluginVersion: {backupDto.PluginVersion ?? "Unknown"}");

            var sourceToRestore = backupDto.Data.First();
            var mediaSourceInfo = sourceToRestore.MediaSourceInfo;
            var chaptersToRestore = sourceToRestore.Chapters ?? new List<ChapterInfo>();

            if (mediaSourceInfo == null)
            {
                _logger.Warn($"[EverMediaService] MediaSourceInfo in medinfo file {medInfoPath} is null.");
                return false;
            }

            item.Size = mediaSourceInfo.Size.GetValueOrDefault();
            item.RunTimeTicks = mediaSourceInfo.RunTimeTicks;
            item.Container = mediaSourceInfo.Container;
            item.TotalBitrate = mediaSourceInfo.Bitrate.GetValueOrDefault();

            var videoStream = mediaSourceInfo.MediaStreams
                .Where(s => s.Type == MediaStreamType.Video && s.Width.HasValue && s.Height.HasValue)
                .OrderByDescending(s => (long)s.Width!.Value * s.Height!.Value)
                .FirstOrDefault();

            if (videoStream != null)
            {
                item.Width = videoStream.Width.GetValueOrDefault();
                item.Height = videoStream.Height.GetValueOrDefault();
            }

            var streamsToSave = mediaSourceInfo.MediaStreams?.ToList() ?? new List<MediaStream>();
            foreach (var stream in streamsToSave.Where(s => s.IsExternal && s.Type == MediaStreamType.Subtitle && s.Path != null))
            {
                stream.Path = Path.Combine(item.ContainingFolderPath, stream.Path);
            }

            // 调用 SaveMediaStreams
            _logger.Debug($"[EverMediaService] Saving {streamsToSave.Count} media streams for item: {item.Path ?? item.Name}");
            // 使用 item.InternalId
            _itemRepository.SaveMediaStreams(item.InternalId, streamsToSave, CancellationToken.None);

            foreach (var chapter in chaptersToRestore)
            {
                chapter.ImageTag = null;    // 清空图片标签，避免上下文问题
            }

            _logger.Debug($"[EverMediaService] Saving {chaptersToRestore.Count} chapters for item: {item.Path ?? item.Name}");
            _itemRepository.SaveChapters(item.InternalId, false, chaptersToRestore);
            // 更新项目并通知 (使用 ILibraryManager)
            _libraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataImport, null);
            _logger.Info($"[EverMediaService] Restore completed successfully for item: {item.Path ?? item.Name}. File used: {medInfoPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMediaService] Error during RestoreAsync for item {item.Path ?? item.Name}: {ex.Message}");
            _logger.Debug(ex.StackTrace);
            return false;
        }
    }

    // --- 重构后的 GetMedInfoPath：支持多路径库 + 手动相对路径 ---
    public string GetMedInfoPath(BaseItem item)
    {
        if (string.IsNullOrEmpty(item.Path))
        {
            _logger.Warn($"[EverMediaService] Item path is null or empty for ID: {item.Id}. Using fallback path.");
            string fallbackDir = item.ContainingFolderPath ?? string.Empty;
            return Path.Combine(fallbackDir, item.Id.ToString() + ".medinfo");
        }

        var config = GetConfiguration() ?? new PluginConfiguration();
        string fileName = Path.GetFileNameWithoutExtension(item.Path) + ".medinfo";

        if (config.BackupMode == BackupMode.Centralized && !string.IsNullOrWhiteSpace(config.CentralizedRootPath))
        {
            var libraryOptions = _libraryManager.GetLibraryOptions(item);
            if (libraryOptions?.PathInfos != null)
            {
                // 查找匹配的媒体库路径（大小写不敏感）
                var matchingPathInfo = libraryOptions.PathInfos
                    .FirstOrDefault(pi => !string.IsNullOrEmpty(pi.Path) &&
                                          item.Path.StartsWith(pi.Path, StringComparison.OrdinalIgnoreCase));

                if (matchingPathInfo != null)
                {
                    string baseLibraryPath = matchingPathInfo.Path;
                    string relativeDir = item.ContainingFolderPath;

                    // 计算相对于媒体库根目录的路径
                    if (relativeDir.Length > baseLibraryPath.Length)
                    {
                        relativeDir = relativeDir.Substring(baseLibraryPath.Length)
                                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }
                    else
                    {
                        relativeDir = string.Empty; // 文件在库根目录
                    }

                    string targetDir = string.IsNullOrEmpty(relativeDir)
                        ? config.CentralizedRootPath
                        : Path.Combine(config.CentralizedRootPath, relativeDir);

                    return Path.Combine(targetDir, fileName);
                }
            }
        }

        // 默认：Side-by-side 模式
        return Path.Combine(item.ContainingFolderPath, fileName);
    }

    // --- 内部 DTO 类 ---
    private class BackupDto
    {
        public string? EmbyVersion { get; set; }
        public string? PluginVersion { get; set; }
        public MediaSourceWithChapters[] Data { get; set; } = Array.Empty<MediaSourceWithChapters>();
    }

    internal class MediaSourceWithChapters
    {
        public MediaSourceInfo? MediaSourceInfo { get; set; }
        public List<ChapterInfo> Chapters { get; set; } = new List<ChapterInfo>();
        public bool? ZeroFingerprintConfidence { get; set; }
        public string? EmbeddedImage { get; set; }
    }
}
