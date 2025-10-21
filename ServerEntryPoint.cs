using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using System;
using System.Threading.Tasks;

namespace evermedia
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        
        public ServerEntryPoint(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }
        
        public void Run()
        {
            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemUpdated += OnItemUpdated;
        }
        
        private async void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            await HandleStrmItemAsync(e.Item);
        }
        
        private async void OnItemUpdated(object sender, ItemChangeEventArgs e)
        {
            await HandleStrmItemAsync(e.Item);
        }
        
        private async Task HandleStrmItemAsync(BaseItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Path))
                return;
                
            if (!item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return;
                
            var logger = MediaBrowser.Common.ServiceServiceContainer.Resolve<MediaBrowser.Common.Logging.ILogManager>()
                .GetLogger("evermedia");
            logger.Info($"Detected .strm file: {item.Path}");
        }
        
        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnItemAdded;
            _libraryManager.ItemUpdated -= OnItemUpdated;
        }
    }
}
