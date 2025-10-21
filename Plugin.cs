using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace evermedia
{
    /// <summary>
    /// The main plugin class for evermedia.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public override string Name => "evermedia";

        /// <summary>
        /// Gets the unique identifier for the plugin.
        /// </summary>
        public override Guid Id => Guid.Parse("a1d6df33-2a66-455b-9b48-527b37e402d4");

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <param name="xmlSerializer">The XML serializer.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the singleton instance of the plugin.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        public override string Description => "Automatically fetch, persist, and restore MediaInfo for .strm files.";
    }

    /// <summary>
    /// The plugin's configuration class (currently empty).
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Configuration can be added here later if needed.
    }
}
