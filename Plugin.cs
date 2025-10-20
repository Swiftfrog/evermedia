#nullable enable

using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using Microsoft.Extensions.DependencyInjection; // You will likely need this using directive
using MediaBrowser.Controller.Plugins; // For IPluginServiceRegistrator
using MediaBrowser.Controller.Providers; // For ICustomMetadataProvider

// Assuming your provider class is in this namespace
using EmbyMedia.Plugin; 

namespace EmbyPinyinPlugin
{
    // Implement IPluginServiceRegistrator
    public class Plugin : BasePlugin<PluginConfiguration>, IPlugin, IPluginServiceRegistrator
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
        }

        public override string Name => "EmbyMedia";

        public override Guid Id => Guid.Parse("91EE5054-84C7-76DF-61BE-CC0A35F6625E"); // Make sure you have a unique GUID

        public override string Description => "A custom metadata provider for MediaInfo.";

        /// <summary>
        /// This method will be called by Emby at startup to let your plugin register its services.
        /// </summary>
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            // Register your provider with the dependency injection container.
            // We register it as a Scoped service, which is a safe default for providers.
            // This tells the DI system: "When someone asks for an ICustomMetadataProvider<Video>, 
            // create an instance of MediaInfoCustomMetadataProvider for them."
            serviceCollection.AddScoped<ICustomMetadataProvider, MediaInfoCustomMetadataProvider>();
            
            // If your provider is strongly typed (e.g., ICustomMetadataProvider<Video>),
            // you might register it like this instead:
            // serviceCollection.AddScoped<ICustomMetadataProvider<Video>, MediaInfoCustomMetadataProvider>();
        }
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        // Configuration options here
    }
}
