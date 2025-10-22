// Plugin.cs
using MediaBrowser.Common.Plugins; // 包含 BasePlugin
using MediaBrowser.Model.Plugins;
using System.Reflection;
using MediaBrowser.Common.Configuration; // 需要 IApplicationPaths
using MediaBrowser.Model.Serialization;  // 需要 IXmlSerializer
// 添加对配置类的引用
using EverMedia.Configuration;

namespace EverMedia;

/// <summary>
/// EverMedia 插件的主入口点。
/// 继承 BasePlugin<PluginConfiguration> 以支持配置。
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration> // ✅ 关键修改：继承 BasePlugin<PluginConfiguration>
{
    // --- BasePlugin<PluginConfiguration> 会自动提供以下属性和方法 ---
    // public override string Name => ... (需要重写)
    // public override string Description => ... (需要重写)
    // public override Guid Id => ... (需要重写)
    // public PluginConfiguration Configuration { get; set; } (由基类提供)

    // --- BasePlugin<PluginConfiguration> 要求重写的属性 ---
    public override string Name => "EverMedia";
    public override string Description => "Self-healing MediaInfo persistence for .strm files.";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    // BasePlugin 已经处理了 Version, AssemblyFilePath, DataFolderPath, GetPluginInfo, OnUninstalling

    // --- 插件生命周期方法与配置 ---
    public static Plugin Instance { get; private set; } = null!;

    // ✅ 添加构造函数，接收依赖注入的服务
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) // 接收服务
        : base(applicationPaths, xmlSerializer) // 调用基类构造函数
    {
        // 在这里可以进行一些基于构造函数参数的初始化，如果需要的话
        // 但通常 BasePlugin 已经处理了配置的加载
    }

    /// <summary>
    /// 当 Emby 应用程序启动时调用。
    /// </summary>
    public void OnApplicationStartup()
    {
        Instance = this; // 设置静态实例以便其他部分访问

        // Configuration 属性已由 BasePlugin 在构造后自动加载
        // var assembly = Assembly.GetExecutingAssembly();
        // AssemblyFilePath = assembly.Location; // 这行现在不需要了，BasePlugin 会处理
    }

    /// <summary>
    /// 当 Emby 应用程序关闭时调用。
    /// </summary>
    public void OnApplicationShutdown()
    {
        // 在第一步中，我们暂时不执行任何操作
    }
}
