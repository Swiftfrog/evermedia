using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;       // LibraryOptions
using MediaBrowser.Model.Providers;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class StrmMetadataProvider : ICustomMetadataProvider<Video>
    {
        private readonly MediaInfoService _mediaInfoService;

        public StrmMetadataProvider(MediaInfoService mediaInfoService)
        {
            _mediaInfoService = mediaInfoService;
        }

        public string Name => "evermedia STRM Probe Provider";

        public async Task<ItemUpdateType> FetchAsync(
            MetadataResult<Video> result,
            MetadataRefreshOptions options,
            LibraryOptions libraryOptions,
            CancellationToken cancellationToken)
        {
            var item = result.Item;
            if (item.Path?.EndsWith(".strm", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (options.MetadataRefreshMode == MetadataRefreshMode.FullRefresh ||
                    options.ReplaceAllMetadata)
                {
                    if (await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken))
                        return ItemUpdateType.MetadataImport;
                }
            }
            return ItemUpdateType.None;
        }
    }
}
