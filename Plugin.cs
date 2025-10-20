using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace EmbyMedia.Plugin;

public class Plugin : BasePlugin, IPluginServices
{
    public override string Name => "EmbyMedia";

    public override Guid Id => Guid.Parse("BCBA4347-E0B1-1801-8459-EEE952DAC618"); // Generate a unique GUID

    public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer) : base(appPaths, xmlSerializer)
    {
    }

    public void RegisterServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMediaInfoService, MediaInfoService>();
        serviceCollection.AddSingleton<ICustomMetadataProvider<Video>, MediaInfoCustomMetadataProvider>();
        serviceCollection.AddSingleton<IScheduledTask, MediaInfoRestoreTask>();
    }
}
