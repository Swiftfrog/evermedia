using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using System.Threading.Tasks;

namespace evermedia
{
    public class StrmMediaInfoBackupService : IItemUpdated
    {
        private readonly MediaInfoService _mediaInfoService;

        public StrmMediaInfoBackupService(MediaInfoService mediaInfoService)
        {
            _mediaInfoService = mediaInfoService;
        }

        public async Task OnItemUpdated(BaseItem item, ItemUpdateType updateType)
        {
            if (item is Video video &&
                video.Path?.EndsWith(".strm", System.StringComparison.OrdinalIgnoreCase) == true &&
                (updateType.HasFlag(ItemUpdateType.ItemCreated) || updateType.HasFlag(ItemUpdateType.MetadataImport)))
            {
                await _mediaInfoService.BackupMediaInfoAsync(video, System.Threading.CancellationToken.None);
            }
        }
    }
}
