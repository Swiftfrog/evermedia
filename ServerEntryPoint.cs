using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace evermedia
{
    /// <summary>
    /// The server entry point for the evermedia plugin.
    /// </summary>
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly MediaInfoService _mediaInfoService;
        private ILogger _logger = null!; // Non-null after Run()

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerEntryPoint"/> class.
        /// </summary>
        public ServerEntryPoint(ILibraryManager libraryManager, MediaInfoService mediaInfoService)
        {
            _libraryManager = libraryManager;
            _mediaInfoService = mediaInfoService;
        }

        /// <summary>
        /// Called when the plugin is loaded.
        /// </summary>
        public void Run()
        {
            _logger = Plugin.Instance.Logger;
            _logger.Info("evermedia: Initializing...");

            _libraryManager.ItemAdded += OnLibraryManagerItemChanged;
            _libraryManager.ItemUpdated += OnLibraryManagerItemChanged;

            _logger.Info("evermedia: Ready and listening for item events.");
        }

        /// <summary>
        /// Handles both ItemAdded and ItemUpdated events.
        /// </summary>
        private void OnLibraryManagerItemChanged(object? sender, ItemChangeEventArgs e)
        {
            ProcessStrmItem(e.Item);
        }

        /// <summary>
        /// Processes a .strm item by probing its real media source and backing up MediaInfo.
        /// </summary>
        private async void ProcessStrmItem(BaseItem? item)
        {
            try
            {
                await _mediaInfoService.BackupMediaInfoAsync(item);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"evermedia: Error in event handler for item '{item?.Name}'.");
            }
        }

        /// <summary>
        /// Called when the plugin is unloaded.
        /// </summary>
        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnLibraryManagerItemChanged;
            _libraryManager.ItemUpdated -= OnLibraryManagerItemChanged;
        }
    }
}
