#nullable enable

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin
{
    /// <summary>
    /// 自定义元数据提供者 - 处理 STRM 文件的 MediaInfo 探测和备份
    /// 注意：必须实现 ICustomMetadataProvider<Video> 而不是具体的 Movie 或 Episode
    /// </summary>
    public class MediaInfoCustomMetadataProvider : ICustomMetadataProvider<Video>, IHasOrder
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly MediaInfoService _mediaInfoService;

        public MediaInfoCustomMetadataProvider(
            ILibraryManager libraryManager,
            ILogger logger,
            IMediaSourceManager mediaSourceManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediaSourceManager = mediaSourceManager ?? throw new ArgumentNullException(nameof(mediaSourceManager));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            
            _mediaInfoService = new MediaInfoService(
                logger,
                libraryManager,
                mediaSourceManager,
                itemRepository,
                fileSystem
            );
            
            _logger.Info("EmbyMedia Plugin: MediaInfoCustomMetadataProvider initialized");
        }

        public string Name => "EmbyMedia Metadata Provider";

        // 设置较高的优先级，确保在其他 provider 之后运行
        public int Order => 100;

        public async Task<ItemUpdateType> FetchAsync(
            MetadataResult<Video> itemResult,
            MetadataRefreshOptions options,
            LibraryOptions libraryOptions,
            CancellationToken cancellationToken)
        {
            var item = itemResult.Item;

            _logger.Info("EmbyMedia Plugin: Processing item {0}", item.Path ?? "unknown");

            bool probeResult = false;
            bool backupResult = false;

            try
            {
                // Check if it's a STRM file and needs probing
                if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info("EmbyMedia Plugin: Detected STRM file {0}", item.Path);
                    probeResult = await _mediaInfoService.EnsureStrmMediaInfoAsync(item, cancellationToken);
                    if (probeResult)
                    {
                        _logger.Info("EmbyMedia Plugin: STRM file {0} MediaInfo updated successfully", item.Path);
                    }
                    else
                    {
                        _logger.Info("EmbyMedia Plugin: STRM file {0} already has MediaInfo or probe failed", item.Path);
                    }
                }

                // Always attempt to backup the current MediaInfo
                backupResult = await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken);
                if (backupResult)
                {
                    _logger.Info("EmbyMedia Plugin: MediaInfo backup completed for {0}", item.Path ?? "unknown");
                }
                else
                {
                    _logger.Debug("EmbyMedia Plugin: No MediaInfo to backup for {0}", item.Path ?? "unknown");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("EmbyMedia Plugin: Error processing item {0}", ex, item.Path ?? "unknown");
            }

            if (probeResult || backupResult)
            {
                return ItemUpdateType.MetadataEdit;
            }
            
            return ItemUpdateType.None;
        }
    }
}
