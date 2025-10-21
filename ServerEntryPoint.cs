using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace evermedia
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly MediaInfoService _mediaInfoService;
        private readonly ILogManager _logManager;
        private ILogger _logger = null!;

        public ServerEntryPoint(
            ILibraryManager libraryManager,
            MediaInfoService mediaInfoService,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaInfoService = mediaInfoService;
            _logManager = logManager;
        }

        public void Run()
        {
            _logger = _logManager.GetLogger("evermedia");
            _logger.Info("evermedia: Initializing...");

            _libraryManager.ItemAdded += OnLibraryManagerItemChanged;
            _libraryManager.ItemUpdated += OnLibraryManagerItemChanged;

            _logger.Info("evermedia: Ready and listening for item events.");
        }

        private void OnLibraryManagerItemChanged(object? sender, ItemChangeEventArgs e)
        {
            ProcessStrmItem(e.Item);
        }

        private async void ProcessStrmItem(BaseItem? item)
        {
            if (item is null) return; // 避免 CS8604 警告

            try
            {
                await _mediaInfoService.BackupMediaInfoAsync(item);
            }
            catch (Exception ex)
            {
                _logger.Error($"evermedia: Error processing item '{item.Name}'. {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnLibraryManagerItemChanged;
            _libraryManager.ItemUpdated -= OnLibraryManagerItemChanged;
        }
    }
}
