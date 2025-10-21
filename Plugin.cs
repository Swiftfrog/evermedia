using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;

namespace evermedia
{
    public class EverMediaPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        // 🔧 修复 CS8618: Instance 在构造函数中赋值，但编译器无法保证非 null
        // 方案：标记为可空，或确保构造时赋值（推荐后者）
        public static EverMediaPlugin? Instance { get; private set; }

        public override string Name => "EverMedia";

        public EverMediaPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this; // 构造函数中赋值，安全
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "evermedia",
                    EmbeddedResourcePath = "evermedia.ConfigurationPage.html"
                }
            };
        }
    }
}
