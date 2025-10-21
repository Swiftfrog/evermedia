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
using MediaBrowser.Model.Providers; // 👈 MetadataRefreshOptions
using MediaBrowser.Model.Serialization;

namespace evermedia
{
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogManager _logManager;

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
            _logManager = logManager;
        }

        public async Task BackupMediaInfoAsync(BaseItem item)
        {
            if (item?.Path is null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return;

            var logger = _logManager.GetLogger("evermedia");
            try
            {
                string realMediaPath = await _fileSystem.ReadAllTextAsync(item.Path, CancellationToken.None);
                realMediaPath = realMediaPath.Trim();
                if (string.IsNullOrEmpty(realMediaPath))
                {
                    logger.Warn($"evermedia: .strm file '{item.Path}' is empty.");
                    return;
                }

                var tempItem = new Video { Path = realMediaPath, IsVirtualItem = false };
                var libraryOptions = _libraryManager.GetLibraryOptions(item);

                MediaSourceInfo? mediaSource = null;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var sources = _mediaSourceManager.GetStaticMediaSources(
                        tempItem,
                        enableAlternateMediaSources: false,
                        enablePathSubstitution: false,
                        fillChapters: false,
                        fillMediaStreams: true, // 👈 新增参数
                        collectionFolders: Array.Empty<BaseItem>(),
                        libraryOptions: libraryOptions,
                        deviceProfile: null,
                        user: null,
                        cancellationToken: cts.Token
                    );
                    mediaSource = sources.FirstOrDefault();
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

                if (mediaSource is null || mediaSource.RunTimeTicks <= 0 || mediaSource.MediaStreams?.Count == 0)
                {
                    logger.Warn($"evermedia: Probe did not yield valid MediaInfo for '{item.Path}'.");
                    return;
                }

                var sanitizedSource = new MediaSourceInfo
                {
                    Protocol = mediaSource.Protocol,
                    Container = mediaSource.Container,
                    RunTimeTicks = mediaSource.RunTimeTicks,
                    Bitrate = mediaSource.Bitrate,
                    Size = mediaSource.Size,
                    MediaStreams = mediaSource.MediaStreams,
                };

                string medinfoPath = Path.ChangeExtension(item.Path, ".medinfo");
                _jsonSerializer.SerializeToFile(sanitizedSource, medinfoPath);
                logger.Info($"evermedia: Wrote .medinfo file for '{item.Name}'.");

                // Persist to database
                item.RunTimeTicks = sanitizedSource.RunTimeTicks;
                item.Container = sanitizedSource.Container;
                item.TotalBitrate = sanitizedSource.Bitrate ?? 0;

                var videoStream = sanitizedSource.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                if (videoStream != null)
                {
                    item.Width = videoStream.Width ?? 0;
                    item.Height = videoStream.Height ?? 0;
                }

                _itemRepository.SaveMediaStreams(item.InternalId, sanitizedSource.MediaStreams, CancellationToken.None);

                // Create MetadataRefreshOptions
                var options = new MetadataRefreshOptions(new DirectoryService(_fileSystem));
                _libraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataEdit, options);

                logger.Info($"evermedia: Persisted MediaInfo to database for '{item.Name}'.");
            }
            catch (Exception ex)
            {
                logger.Error($"evermedia: Unexpected error for '{item?.Name}'. {ex.Message}", ex);
            }
        }
    }
}
