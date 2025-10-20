using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System;
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
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IJsonSerializer _jsonSerializer;

        private const string MediaInfoExtension = ".medinfo";

        public MediaInfoService(
            ILogger logger,
            ILibraryManager libraryManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem,
            IMediaEncoder mediaEncoder,
            IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _mediaEncoder = mediaEncoder;
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
                        MediaSource = new MediaSourceInfo { Path = realPath }
                        // 注意：Emby 4.9 不需要 MediaType
                    };
                    var result = await _mediaEncoder.GetMediaInfo(request, ct);
                    mediaSource = result.MediaSource;
                }

                // 保存 MediaStreams 到数据库
                _itemRepository.SaveMediaStreams(item.InternalId, mediaSource.MediaStreams, ct);

                // 从视频流中提取分辨率
                var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                if (videoStream != null)
                {
                    item.Width = videoStream.Width ?? 0;
                    item.Height = videoStream.Height ?? 0;
                }

                item.RunTimeTicks = mediaSource.RunTimeTicks;
                item.Container = mediaSource.Container;
                item.TotalBitrate = mediaSource.Bitrate ?? 0;

                _libraryManager.UpdateItems(new[] { item }, null, ItemUpdateType.MetadataImport, false, false, null, ct);

                // 使用 Emby 内置的 JsonSerializer
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
                var mediaSource = _jsonSerializer.DeserializeFromFile<MediaSourceInfo>(backupPath);

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

                    _libraryManager.UpdateItems(new[] { item }, null, ItemUpdateType.MetadataImport, false, false, null, ct);
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
