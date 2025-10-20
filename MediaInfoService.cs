// StrmMetadataProvider.cs
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Providers;
using System.Threading.Tasks;
using System.Threading;

namespace evermedia
{
    public class StrmMetadataProvider : ICustomMetadataProvider<Video>
    {
        private readonly MediaInfoService _mediaInfoService;

        public StrmMetadataProvider(MediaInfoService mediaInfoService)
        {
            _mediaInfoService = mediaInfoService;
        }

        public string Name => "evermedia STRM Provider";

        public async Task<ItemUpdateType> FetchAsync(
            MetadataResult<Video> result,
            MetadataRefreshOptions options,
            CancellationToken cancellationToken)
        {
            var item = result.Item;
            if (item.Path?.EndsWith(".strm", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken))
                    return ItemUpdateType.MetadataImport;
            }
            return ItemUpdateType.None;
        }
    }
}
