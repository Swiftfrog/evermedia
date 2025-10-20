#nullable enable

using MediaBrowser.Controller.Entities;
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
    public class MediaInfoCustomMetadataProvider : ICustomMetadataProvider<Video>, IHasOrder
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly MediaInfoService _mediaInfoService;

        /// <summary>
        /// 构造函数 - Emby 4.9 会自动注入这些依赖
        /// </summary>
        public MediaInfoCustomMetadataProvider(
            ILibraryManager libraryManager,
            ILogger logger,
            IMediaSourceManager mediaSourceManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _mediaSourceManager = mediaSourceManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            
            // 手动创建 MediaInfoService 实例
            _mediaInfoService = new MediaInfoService(
                logger,
                libraryManager,
                mediaSourceManager,
                itemRepository,
                fileSystem
            );
        }

        public string Name => "EmbyMedia Metadata Provider";

        public int Order => 0;

        public async Task<ItemUpdateType> FetchAsync(
            MetadataResult<Video> itemResult,
            MetadataRefreshOptions options,
            LibraryOptions libraryOptions,
            CancellationToken cancellationToken)
        {
            var item = itemResult.Item;

            _logger.Debug("EmbyMedia: Processing item {0}", item.Path ?? "unknown");

            bool probeResult = false;

            // Check if it's a STRM file and needs probing
            if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug("EmbyMedia: Processing STRM file {0}", item.Path);
                probeResult = await _mediaInfoService.EnsureStrmMediaInfoAsync(item, cancellationToken);
                if (probeResult)
                {
                    _logger.Info("EmbyMedia: STRM file {0} MediaInfo updated", item.Path);
                }
            }

            // Always attempt to backup the current MediaInfo
            var backupResult = await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken);
            if (backupResult)
            {
                _logger.Debug("EmbyMedia: MediaInfo backup completed for {0}", item.Path ?? "unknown");
            }

            if (probeResult || backupResult)
            {
                return ItemUpdateType.MetadataEdit;
            }
            
            return ItemUpdateType.None;
        }
    }
}
