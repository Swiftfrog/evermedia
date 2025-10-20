#nullable enable

using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins; // For BasePlugin
using MediaBrowser.Model.Serialization; // For IXmlSerializer (if needed for config, but not for provider registration)
using System;

namespace EmbyMedia.Plugin;

/// <summary>
/// Main plugin class for EmbyMedia.
/// Defines basic plugin metadata like Name and Id.
/// The actual registration of custom providers happens in ServerEntryPoint.cs via IServerEntryPoint.
/// </summary>
public class Plugin : BasePlugin // Inherits from BasePlugin to provide basic plugin info
{
    // Use a constant or a static readonly field for the GUID to ensure consistency
    // Generate a new GUID for your plugin (e.g., using Visual Studio's Tools -> Create GUID)
    private static readonly Guid PluginId = Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489"); // Replace with YOUR generated GUID

    public override string Name => "EmbyMedia"; // The display name of your plugin

    public override Guid Id => PluginId; // The unique identifier for your plugin

    // The constructor for BasePlugin can vary depending on the version and needs.
    // Common constructors include:
    // - BasePlugin() - No parameters (as per API doc)
    // - BasePlugin(IApplicationPaths applicationPaths) - If you need access to paths in the constructor
    // - BasePlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) - Less common now, often for config

    // For this plugin, if you don't need paths or serialization in the Plugin class constructor itself,
    // using the parameterless constructor is fine.
    // If you do need IApplicationPaths for something specific in Plugin (e.g., setting up config paths),
    // you can use the constructor that takes it.
    // The key point is: registering providers doesn't happen here.

    public Plugin() : base() // Use the public parameterless constructor from BasePlugin
    {
        // Initialization logic specific to the Plugin class instance can go here if needed.
        // However, for registering providers, ServerEntryPoint is the correct place.
        // Example: You might initialize plugin-specific settings paths here if not using BasePlugin's built-in config handling.
    }

    // Optional: Override Description if you want a custom description
    // public override string Description => "Provides media info backup and restore functionality.";

    // Optional: Override Version if it's not automatically derived from assembly info correctly by BasePlugin
    // public override Version Version => typeof(Plugin).Assembly.GetName().Version;
}
