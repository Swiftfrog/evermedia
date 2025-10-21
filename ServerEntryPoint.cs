using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace evermedia
{
    /// <summary>
    /// The server entry point for the evermedia plugin.
    /// Handles initialization and event subscription.
    /// </summary>
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogManager _logManager;
        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerEntryPoint"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="logManager">The log manager.</param>
        public ServerEntryPoint(ILibraryManager libraryManager, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _logManager = logManager;
        }

        /// <summary>
        /// Called when the plugin is loaded.
        /// </summary>
        public void Run()
        {
            _logger = _logManager.GetLogger(GetType().Name);
            _logger.Info("evermedia: Initializing...");

            // Subscribe to library events
            _libraryManager.ItemAdded += OnLibraryManagerItemAdded;
            _libraryManager.ItemUpdated += OnLibraryManagerItemUpdated;

            _logger.Info("evermedia: Ready and listening for item events.");
        }

        /// <summary>
        /// Handles the ItemAdded event.
        /// </summary>
        private void OnLibraryManagerItemAdded(object sender, ItemChangeEventArgs e)
        {
            ProcessStrmItem(e.Item);
        }

        /// <summary>
        /// Handles the ItemUpdated event.
        /// </summary>
        private void OnLibraryManagerItemUpdated(object sender, ItemChangeEventArgs e)
        {
            ProcessStrmItem(e.Item);
        }

        /// <summary>
        /// Processes a .strm item by reading its content and writing a .medinfo file.
        /// </summary>
        /// <param name="item">The item to process.</param>
        private async void ProcessStrmItem(BaseItem item)
        {
            try
            {
                if (item is null || string.IsNullOrEmpty(item.Path))
                {
                    return;
                }

                if (!item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _logger.Info($"evermedia: Processing .strm file: '{item.Name}' at '{item.Path}'.");

                // Step 1: Read the real media path from the .strm file
                string realMediaPath = await File.ReadAllTextAsync(item.Path);
                realMediaPath = realMediaPath.Trim();

                if (string.IsNullOrEmpty(realMediaPath))
                {
                    _logger.Warn($"evermedia: .strm file '{item.Path}' is empty.");
                    return;
                }

                // Step 2: Define the .medinfo backup path
                string medinfoPath = Path.ChangeExtension(item.Path, ".medinfo");

                // Step 3: Write a simple JSON placeholder to the .medinfo file
                // In Phase 3, this will be replaced with the actual MediaSourceInfo JSON.
                string jsonContent = $"{{\"RealMediaPath\": \"{realMediaPath}\"}}";
                await File.WriteAllTextAsync(medinfoPath, jsonContent);

                _logger.Info($"evermedia: Successfully wrote .medinfo file for '{item.Name}'.");
            }
            catch (Exception ex)
            {
                _logger.Error($"evermedia: Error processing item '{item?.Name}'.", ex);
            }
        }

        /// <summary>
        /// Called when the plugin is unloaded.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from events to prevent memory leaks
            _libraryManager.ItemAdded -= OnLibraryManagerItemAdded;
            _libraryManager.ItemUpdated -= OnLibraryManagerItemUpdated;
        }
    }
}
