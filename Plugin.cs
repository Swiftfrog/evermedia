// Plugin.cs
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace EmbyMedia.Plugin
{
    /// <summary>
    /// 插件主类，定义插件的基本信息和配置。
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        private static Plugin? _instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">应用程序路径。</param>
        /// <param name="xmlSerializer">XML 序列化器。</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            _instance = this;
        }

        /// <summary>
        /// 获取插件单例实例
        /// </summary>
        public static Plugin? Instance => _instance;

        /// <inheritdoc />
        public override string Name => "EmbyMedia Plugin";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("91EE5054-84C7-76DF-61BE-CC0A35F6625E");

        /// <inheritdoc />
        public override string Description => "Provides enhanced MediaInfo handling for STRM files and backups/restores metadata.";
    }

    /// <summary>
    /// 插件配置类
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        // 可以在这里添加插件的配置选项
        // 例如: public bool EnableAutomaticBackup { get; set; } = true;
    }
}
