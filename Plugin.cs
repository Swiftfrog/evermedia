using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;

namespace EverMedia;

/// <summary>
/// EverMedia 插件的主入口点。
/// </summary>
[Plugin(
    name: "EverMedia",
    description: "Persistent MediaInfo for .strm files with self-healing.",
    version: "1.0.0.0",
    targetAbi: "4.9.1.80" // 对应 Emby Server 4.9.1.80
)]
public class Plugin : IPlugin
{
    // 实现 IPlugin 接口所需的属性
    public string Name => "EverMedia";
    public string Description => "Self-healing MediaInfo persistence for .strm files.";
    public string Id => "EverMedia";

    // 可选：用于插件内部访问自身实例
    public static Plugin Instance { get; private set; } = null!;

    // 可选：插件配置（在后续步骤中会用到）
    // public PluginConfiguration Configuration { get; private set; } = new();

    /// <summary>
    /// 当 Emby 应用程序启动时调用。
    /// </summary>
    public void OnApplicationStartup()
    {
        // 在第一步中，我们暂时不执行任何操作
        // 后续步骤将在此处初始化服务、注册事件监听器等
        Instance = this; // 设置静态实例以便其他部分访问
    }

    /// <summary>
    /// 当 Emby 应用程序关闭时调用。
    /// </summary>
    public void OnApplicationShutdown()
    {
        // 在第一步中，我们暂时不执行任何操作
        // 后续步骤可能需要在此处清理资源或取消事件订阅
    }
}
