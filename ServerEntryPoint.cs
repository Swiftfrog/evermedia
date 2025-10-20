#nullable enable

using MediaBrowser.Controller.Plugins; // For IServerEntryPoint
using MediaBrowser.Controller.Providers; // For IProviderManager
using MediaBrowser.Model.Logging; // For ILogger (Emby's ILogger)
using MediaBrowser.Controller.Library; // For ILibraryManager if needed in provider
using System;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

public class ServerEntryPoint : IServerEntryPoint
{
    private readonly IProviderManager _providerManager;
    private readonly ILibraryManager _libraryManager; // If needed by provider
    private readonly ILogger _logger; // Use Emby's ILogger

    public ServerEntryPoint(IProviderManager providerManager, ILibraryManager libraryManager, ILogger logger) // Inject dependencies
    {
        _providerManager = providerManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public async Task RunAsync() // Use RunAsync if available, or Run if not
    {
        _logger.Info("EmbyMedia ServerEntryPoint RunAsync started.");

        try
        {
            // Create an instance of your custom metadata provider, passing required dependencies
            var customMetadataProvider = new MediaInfoCustomMetadataProvider(_libraryManager, _logger);

            // Register the provider with the ProviderManager
            // Check the exact method name in IProviderManager API for 4.9.1.80
            // Commonly it might be AddProvider, or a specific collection property.
            // Assuming AddProvider exists and accepts ICustomMetadataProvider<T>:
            _providerManager.AddProvider(customMetadataProvider);

            _logger.Info("EmbyMedia MediaInfoCustomMetadataProvider registered successfully.");
        }
        catch (Exception ex)
        {
             _logger.ErrorException("Error registering EmbyMedia MediaInfoCustomMetadataProvider: {0}", ex, ex.Message);
        }

        await Task.CompletedTask; // If RunAsync is used, ensure it returns Task
    }

    public void Dispose()
    {
        // Perform cleanup if needed
        _logger.Info("EmbyMedia ServerEntryPoint Disposed.");
    }
}
