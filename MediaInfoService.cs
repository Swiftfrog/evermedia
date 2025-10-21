using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Providers; // MetadataRefreshOptions
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.MediaEncoding; // IMediaEncoder
using MediaBrowser.Model.Dlna;              // DlnaProfileType


namespace evermedia
{
    /// <summary>
    /// Core service for probing, persisting, and restoring MediaInfo for .strm files.
    /// </summary>
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogManager _logManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IMediaEncoder _mediaEncoder;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInfoService"/> class.
        /// </summary>
        public MediaInfoService(
            ILibraryManager libraryManager,
            IMediaEncoder mediaEncoder,
            IItemRepository itemRepository,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogManager logManager
            IMediaEncoder _mediaEncoder;)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _jsonSerializer = jsonSerializer;
            _logManager = logManager;
            _mediaEncoder = mediaEncoder;
        }

        /// <summary>
        /// Backs up the MediaInfo for a .strm item by probing its real media source,
        /// persisting to a .medinfo file, and updating the Emby database.
        /// </summary>
        /// <param name="item">The .strm item to process.</param>
        public async Task BackupMediaInfoAsync(BaseItem item)
        {
            if (item?.Path is null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return;

            var logger = _logManager.GetLogger("evermedia");
            try
            {
                // Step 1: Read the real media path from the .strm file
                string realMediaPath = await _fileSystem.ReadAllTextAsync(item.Path, CancellationToken.None);
                realMediaPath = realMediaPath.Trim();

                if (string.IsNullOrEmpty(realMediaPath))
                {
                    logger.Warn($"evermedia: .strm file '{item.Path}' is empty.");
                    return;
                }

                // Step 2: Probe the real media source with a timeout
                MediaSourceInfo? mediaSource = null;
                try
                {

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15-second timeout

                    var request = new MediaInfoRequest
                    {
                        MediaSource = new MediaSourceInfo { Path = realMediaPath },
                        MediaType = DlnaProfileType.Video
                    };

                    var probeResult = await _mediaEncoder.GetMediaInfo(request, cts.Token);
                    mediaSource = probeResult.MediaSource;
                }

                catch (OperationCanceledException)
                {
                    logger.Warn($"evermedia: Probe for '{item.Path}' timed out.");
                    return;
                }
                catch (Exception ex)
                {
                    logger.Error($"evermedia: Probe failed for '{item.Path}'. {ex.Message}", ex);
                    return;
                }

                if (mediaSource?.RunTimeTicks <= 0 || mediaSource.MediaStreams?.Count == 0)
                {
                    logger.Warn($"evermedia: Probe did not yield valid MediaInfo for '{item.Path}'.");
                    return;
                }

                // Step 3: Sanitize the MediaSourceInfo for serialization
                var sanitizedSource = new MediaSourceInfo
                {
                    Protocol = mediaSource.Protocol,
                    Container = mediaSource.Container,
                    RunTimeTicks = mediaSource.RunTimeTicks,
                    Bitrate = mediaSource.Bitrate,
                    Size = mediaSource.Size,
                    MediaStreams = mediaSource.MediaStreams
                    // Note: Critical fields like Id, Path, TranscodingUrl are intentionally omitted
                };

                // Step 4: Write to .medinfo file
                string medinfoPath = Path.ChangeExtension(item.Path, ".medinfo");
                _jsonSerializer.SerializeToFile(sanitizedSource, medinfoPath);
                logger.Info($"evermedia: Wrote .medinfo file for '{item.Name}'.");

                // Step 5: Persist to Emby database
                item.RunTimeTicks = sanitizedSource.RunTimeTicks;
                item.Container = sanitizedSource.Container;
                item.TotalBitrate = sanitizedSource.Bitrate ?? 0;

                var videoStream = sanitizedSource.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                if (videoStream != null)
                {
                    item.Width = videoStream.Width ?? 0;
                    item.Height = videoStream.Height ?? 0;
                }

                _itemRepository.SaveMediaStreams(item.InternalId, sanitizedSource.MediaStreams, CancellationToken.None);

                var options = new MetadataRefreshOptions(new DirectoryService(_fileSystem));
                _libraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataEdit, options);

                logger.Info($"evermedia: Persisted MediaInfo to database for '{item.Name}'.");
            }
            catch (Exception ex)
            {
                var logger = _logManager.GetLogger("evermedia");
                logger.Error($"evermedia: Unexpected error for '{item?.Name}'. {ex.Message}", ex);
            }
        }
    }
}
