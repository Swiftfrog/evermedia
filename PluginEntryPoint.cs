using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace evermedia
{
    public class PluginEntryPoint : IServerEntryPoint
    {
        private readonly ILogger _logger;

        // ✅ 只注入 ILogger（Emby 支持）
        public PluginEntryPoint(ILogger logger)
        {
            _logger = logger;
        }

        public void Run()
        {
            // ❌ 不要注册服务
            // Emby 会自动扫描插件程序集并注册 StrmMetadataProvider、StrmMediaSourceProvider、MediaInfoRestoreTask

            _logger.Info("evermedia: Plugin loaded successfully");
        }

        public void Dispose()
        {
            _logger.Info("evermedia: Plugin shutting down");
        }
    }
}
