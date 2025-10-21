using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Common.Logging;
using System;
using System.Threading.Tasks;

namespace evermedia
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger; // ← 新增

        // ← 注入 ILogManager
        public ServerEntryPoint(ILibraryManager libraryManager, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _logger = logManager.GetLogger(nameof(ServerEntryPoint)); // ← 创建 logger
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
                
            // ✅ 直接使用 _logger，不再通过 ServiceServiceContainer
            _logger.Info($"Detected .strm file: {item.Path}");
        }
        
        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnItemAdded;
            _libraryManager.ItemUpdated -= OnItemUpdated;
        }
    }
}
