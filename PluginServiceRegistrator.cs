using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller.Plugins;

namespace evermedia
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection container, IServerApplicationHost applicationHost)
        {
            container.AddSingleton<MediaInfoService>();
            container.AddSingleton<StrmMediaInfoBackupService>();
            container.AddSingleton<IMediaSourceProvider, StrmMediaSourceProvider>();
            container.AddSingleton<IScheduledTask, MediaInfoRestoreTask>();
        }
    }
}
