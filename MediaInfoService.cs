using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;

namespace evermedia
{
    /// <summary>
    /// Core service for probing, persisting, and restoring MediaInfo for .strm files.
    /// </summary>
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInfoService"/> class.
        /// </summary>
        public MediaInfoService(
            ILibraryManager libraryManager,
            IMediaSourceManager mediaSourceManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaSourceManager = mediaSourceManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _jsonSerializer = jsonSerializer;
            _logger = logManager.GetLogger(GetType().Name);
        }

        /// <summary>
        /// Backs up the MediaInfo for a .strm item by probing its real media source,
        /// persisting to a .medinfo file, and updating the Emby database.
        /// </summary>
        /// <param name="item">The .strm item to process.</param>
        public async Task BackupMediaInfoAsync(BaseItem item)
        {
            if (item?.Path is null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                // Step 1: Read the real media path from the .strm file
                string realMediaPath = await _fileSystem.ReadAllTextAsync(item.Path, CancellationToken.None);
                realMediaPath = realMediaPath.Trim();

                if (string.IsNullOrEmpty(realMediaPath))
                {
                    _logger.Warn($"evermedia: .strm file '{item.Path}' is empty.");
                    return;
                }

                // Step 2: Create a temporary Video item for probing
                var tempItem = new Video { Path = realMediaPath, IsVirtualItem = false };
                var libraryOptions = _libraryManager.GetLibraryOptions(item);

                // Step 3: Probe the real media source with a timeout
                MediaSourceInfo? mediaSource = null;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15-second timeout
                    var sources = _mediaSourceManager.GetStaticMediaSources(
                        tempItem,
                        enableAlternateMediaSources: false,
                        enablePathSubstitution: false,
                        fillChapters: false,
                        collectionFolders: Array.Empty<BaseItem>(),
                        libraryOptions: libraryOptions,
                        deviceProfile: null,
                        user: null,
                        cancellationToken: cts.Token
                    );

                    mediaSource = sources.FirstOrDefault();
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    _logger.Warn($"evermedia: Probe for '{item.Path}' timed out.");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"evermedia: Probe failed for '{item.Path}'.");
                    return;
                }

                if (mediaSource is null || mediaSource.RunTimeTicks <= 0 || mediaSource.MediaStreams?.Count == 0)
                {
                    _logger.Warn($"evermedia: Probe did not yield valid MediaInfo for '{item.Path}'.");
                    return;
                }

                // Step 4: Sanitize the MediaSourceInfo for serialization
                var sanitizedSource = new MediaSourceInfo
                {
                    Protocol = mediaSource.Protocol,
                    Container = mediaSource.Container,
                    RunTimeTicks = mediaSource.RunTimeTicks,
                    Bitrate = mediaSource.Bitrate,
                    Size = mediaSource.Size,
                    MediaStreams = mediaSource.MediaStreams,
                    // Note: Critical fields like Id, Path, TranscodingUrl are intentionally omitted
                };

                // Step 5: Write to .medinfo file
                string medinfoPath = Path.ChangeExtension(item.Path, ".medinfo");
                _jsonSerializer.SerializeToFile(sanitizedSource, medinfoPath);
                _logger.Info($"evermedia: Wrote .medinfo file for '{item.Name}'.");

                // Step 6: Persist to Emby database
                await PersistToDatabaseAsync(item, sanitizedSource);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"evermedia: Unexpected error while backing up MediaInfo for '{item?.Name}'.");
            }
        }

        /// <summary>
        /// Persists the MediaSourceInfo to the Emby database.
        /// </summary>
        private async Task PersistToDatabaseAsync(BaseItem item, MediaSourceInfo mediaSource)
        {
            // Update the main item's properties
            item.RunTimeTicks = mediaSource.RunTimeTicks;
            item.Container = mediaSource.Container;
            item.TotalBitrate = mediaSource.Bitrate ?? 0;

            // Extract resolution from the first video stream
            var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (videoStream != null)
            {
                item.Width = videoStream.Width ?? 0;
                item.Height = videoStream.Height ?? 0;
            }

            // Save the media streams to the database
            _itemRepository.SaveMediaStreams(item.InternalId, mediaSource.MediaStreams, CancellationToken.None);

            // Update the item in the database and notify the UI
            var options = new MetadataRefreshOptions(new DirectoryService(_fileSystem));
            _libraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataEdit, options);

            _logger.Info($"evermedia: Persisted MediaInfo to database for '{item.Name}'.");
        }
    }
}
