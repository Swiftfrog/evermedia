using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using System;
using System.IO;
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
        private readonly IMediaEncoder _mediaEncoder;

        private const string MediaInfoExtension = ".medinfo";

        public MediaInfoService(
            ILogger logger,
            ILibraryManager libraryManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem,
            IMediaEncoder mediaEncoder)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _mediaEncoder = mediaEncoder;
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
                MediaSourceInfo mediaSource;
                if (Uri.TryCreate(realPath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    _logger.Debug("evermedia: Skipping probe for remote URL: {Path}", item.Path);
                    return false;
                }
                else
                {
                    var request = new MediaInfoRequest
                    {
                        MediaSource = new MediaSourceInfo { Path = realPath },
                        MediaType = DlnaProfileType.Video
                    };
                    var result = await _mediaEncoder.GetMediaInfo(request, ct);
                    mediaSource = result.MediaSource;
                }

                _itemRepository.SaveMediaStreams(item.Id, mediaSource.MediaStreams, ct);
                item.RunTimeTicks = mediaSource.RunTimeTicks;
                item.Container = mediaSource.Container;
                item.TotalBitrate = mediaSource.Bitrate ?? 0;
                if (mediaSource.Width.HasValue) item.Width = mediaSource.Width.Value;
                if (mediaSource.Height.HasValue) item.Height = mediaSource.Height.Value;

                _libraryManager.UpdateItems([item], null, ItemUpdateType.MetadataImport, false, false, null, ct);

                var backupPath = GetBackupPath(item);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                await File.WriteAllTextAsync(backupPath, Newtonsoft.Json.JsonConvert.SerializeObject(mediaSource, Newtonsoft.Json.Formatting.Indented), ct);

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
                var json = await File.ReadAllTextAsync(backupPath, ct);
                var mediaSource = Newtonsoft.Json.JsonConvert.DeserializeObject<MediaSourceInfo>(json);

                if (mediaSource?.RunTimeTicks > 0 && mediaSource.MediaStreams?.Count > 0)
                {
                    _itemRepository.SaveMediaStreams(item.Id, mediaSource.MediaStreams, ct);
                    item.RunTimeTicks = mediaSource.RunTimeTicks;
                    item.Container = mediaSource.Container;
                    item.TotalBitrate = mediaSource.Bitrate ?? 0;
                    if (mediaSource.Width.HasValue) item.Width = mediaSource.Width.Value;
                    if (mediaSource.Height.HasValue) item.Height = mediaSource.Height.Value;

                    _libraryManager.UpdateItems([item], null, ItemUpdateType.MetadataImport, false, false, null, ct);
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
