using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace evermedia
{
    public class PluginEntryPoint : IServerEntryPoint
    {
        private readonly ILogger _logger;
        private readonly IServiceCollection _serviceCollection;

        public PluginEntryPoint(ILogger logger, IServiceCollection serviceCollection)
        {
            _logger = logger;
            _serviceCollection = serviceCollection;
        }

        public void Run()
        {
            _serviceCollection.AddSingleton<MediaInfoService>();
            _serviceCollection.AddSingleton<ICustomMetadataProvider<Video>, StrmMetadataProvider>();
            _serviceCollection.AddSingleton<IMediaSourceProvider, StrmMediaSourceProvider>();
            _serviceCollection.AddSingleton<IScheduledTask, MediaInfoRestoreTask>();

            _logger.Info("evermedia: Services registered");
        }

        public void Dispose()
        {
            _logger.Info("evermedia: Plugin shutting down");
        }
    }
}
