// Plugin.cs
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection; // Add this
using System;

namespace EmbyMedia.Plugin;

public class Plugin : BasePlugin // Keep BasePlugin for basic info
{
    public override string Name => "EmbyMedia";

    public override Guid Id => Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489");

    // Keep the BasePlugin constructor if needed for basic functionality
    public Plugin() : base() // Use the public parameterless constructor
    {
    }
}

// Add a separate class implementing the registration interface IF IT EXISTS
// Check Emby API for the correct interface name, e.g., IPluginServiceRegistrator or similar
// This is a hypothetical example based on common patterns:
public class EmbyMediaPluginServiceRegistrator : IPluginServiceRegistrator // <-- Check actual interface name
{
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        // Register your custom metadata provider
        serviceCollection.AddSingleton<ICustomMetadataProvider<Video>, MediaInfoCustomMetadataProvider>();
        // Register other services if needed
        serviceCollection.AddSingleton<IMediaInfoService, MediaInfoService>();
    }
}
