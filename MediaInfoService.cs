using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic; 
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class MediaInfoService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly IMediaSourceManager _mediaSourceManager; // 👈 替换 IMediaEncoder
        private readonly IJsonSerializer _jsonSerializer;

        private const string MediaInfoExtension = ".medinfo";

        public MediaInfoService(
            ILogger logger,
            ILibraryManager libraryManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem,
            IMediaSourceManager mediaSourceManager, // 👈
            IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _mediaSourceManager = mediaSourceManager; // 👈
            _jsonSerializer = jsonSerializer;
        }

        public string GetBackupPath(BaseItem item)
        {
            return string.IsNullOrEmpty(item.Path) ? string.Empty : Path.ChangeExtension(item.Path, MediaInfoExtension);
        }

        public async Task<bool> BackupMediaInfoAsync(BaseItem item, CancellationToken ct)
        {
            if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return false;

            var strmContent = await File.ReadAllTextAsync(item.Path, ct);
            var realPath = strmContent.Trim();
            if (string.IsNullOrEmpty(realPath)) return false;

            try
            {
                MediaSourceInfo? mediaSource = null;
                if (Uri.TryCreate(realPath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    _logger.Debug("evermedia: Skipping probe for remote URL: {Path}", item.Path);
                    return false;
                }
                else
                {
                    // 👇 使用 IMediaSourceManager 获取 MediaSourceInfo
                    var tempItem = new Video { Path = realPath, IsVirtualItem = false };
                    var libraryOptions = _libraryManager.GetLibraryOptions(item);
                    var sources = _mediaSourceManager.GetStaticMediaSources(
                        tempItem,
                        enableAlternateMediaSources: false,
                        enablePathSubstitution: false,
                        fillChapters: false,
                        collectionFolders: Array.Empty<BaseItem>(),
                        libraryOptions: libraryOptions,
                        deviceProfile: null,
                        user: null);

                    mediaSource = sources.FirstOrDefault();
                    if (mediaSource == null) return false;
                }

                _itemRepository.SaveMediaStreams(item.InternalId, mediaSource.MediaStreams, ct);

                var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                if (videoStream != null)
                {
                    item.Width = videoStream.Width ?? 0;
                    item.Height = videoStream.Height ?? 0;
                }

                item.RunTimeTicks = mediaSource.RunTimeTicks;
                item.Container = mediaSource.Container;
                item.TotalBitrate = mediaSource.Bitrate ?? 0;

                // 👇 修正：使用 List<BaseItem>
                _libraryManager.UpdateItems(new List<BaseItem> { item }, null, ItemUpdateType.MetadataImport, false, false, null, ct);

                var backupPath = GetBackupPath(item);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                _jsonSerializer.SerializeToFile(mediaSource, backupPath);

                _logger.Info("evermedia: Backed up MediaInfo for {Path} to {Backup}", item.Path, backupPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("evermedia: Failed to backup MediaInfo for {Path}", ex, item.Path);
                return false;
            }
        }

        public async Task<bool> RestoreMediaInfoAsync(BaseItem item, CancellationToken ct)
        {
            var backupPath = GetBackupPath(item);
            if (!File.Exists(backupPath)) return false;

            try
            {
                var mediaSource = _jsonSerializer.DeserializeFromFile<MediaSourceInfo?>(backupPath);

                if (mediaSource?.RunTimeTicks > 0 && mediaSource.MediaStreams?.Count > 0)
                {
                    _itemRepository.SaveMediaStreams(item.InternalId, mediaSource.MediaStreams, ct);

                    var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                    if (videoStream != null)
                    {
                        item.Width = videoStream.Width ?? 0;
                        item.Height = videoStream.Height ?? 0;
                    }

                    item.RunTimeTicks = mediaSource.RunTimeTicks;
                    item.Container = mediaSource.Container;
                    item.TotalBitrate = mediaSource.Bitrate ?? 0;

                    // 👇 修正：使用 List<BaseItem>
                    _libraryManager.UpdateItems(new List<BaseItem> { item }, null, ItemUpdateType.MetadataImport, false, false, null, ct);
                    _logger.Info("evermedia: Restored MediaInfo from {Backup}", backupPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("evermedia: Failed to restore from {Backup}", ex, backupPath);
            }
            return false;
        }
    }
}
