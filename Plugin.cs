// ============================================
// 1. Plugin.cs
// ============================================
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Logging;
using System;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin
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

        public override string Name => "EmbyMedia Plugin";

        public override Guid Id => Guid.Parse("91EE5054-84C7-76DF-61BE-CC0A35F6625E");

        public override string Description => "Provides enhanced MediaInfo handling for STRM files and backups/restores metadata.";
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        // 配置选项
    }

    /// <summary>
    /// 服务器入口点 - 用于记录插件启动信息
    /// </summary>
    public class PluginEntryPoint : IServerEntryPoint
    {
        private readonly ILogger _logger;

        public PluginEntryPoint(ILogger logger)
        {
            _logger = logger;
        }

        public void Run()
        {
            _logger.Info("EmbyMedia Plugin: Plugin loaded successfully");
            _logger.Info("EmbyMedia Plugin: MediaInfoCustomMetadataProvider registered for Video items");
            _logger.Info("EmbyMedia Plugin: MediaInfoRestoreTask registered as scheduled task");
            /// return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.Info("EmbyMedia Plugin: Plugin shutting down");
        }
    }
}

