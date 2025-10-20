using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace evermedia
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        private static Plugin? _instance;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            _instance = this;
        }

        public static Plugin? Instance => _instance;

        public override string Name => "evermedia";
        public override Guid Id => Guid.Parse("91EE5054-84C7-76DF-61BE-CC0A35F6625E");
        public override string Description => "Pre-probe and persist MediaInfo for STRM files.";
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
    }
}
