// Plugin.cs
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using System.Reflection;
// 添加对配置类的引用
using EverMedia.Configuration;

namespace EverMedia;

/// <summary>
/// EverMedia 插件的主入口点。
/// 实现 IPlugin 接口以与 Emby Server 集成。
/// </summary>
public class Plugin : IPlugin
{
    // --- IPlugin 接口必须实现的属性 ---
    public string Name => "EverMedia";
    public string Description => "Self-healing MediaInfo persistence for .strm files.";
    public Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");
    public Version Version => new Version(1, 0, 0, 0);
    public string AssemblyFilePath { get; private set; } = string.Empty;
    public string DataFolderPath { get; private set; } = string.Empty;

    // --- IPlugin 接口必须实现的方法 ---
    public PluginInfo GetPluginInfo()
    {
        return new PluginInfo
        {
            Name = this.Name,
            Description = this.Description,
            Id = this.Id.ToString(),
            Version = this.Version.ToString()
        };
    }

    public void OnUninstalling()
    {
        // 在第一步中，我们暂时不执行任何操作
    }

    // --- 插件生命周期方法与配置 ---
    public static Plugin Instance { get; private set; } = null!;

    // 添加 Configuration 属性
    // Emby 框架会自动处理此属性的加载和保存
    public PluginConfiguration Configuration { get; private set; } = new(); // 初始化为默认值

    /// <summary>
    /// 当 Emby 应用程序启动时调用。
    /// </summary>
    public void OnApplicationStartup()
    {
        Instance = this; // 设置静态实例以便其他部分访问

        // 在这里，Configuration 属性已经由 Emby 自动加载了
        // 如果 config/Plugin/7B921178-7C5B-42D6-BB7C-42E8B00C2C7D.xml 文件存在，Configuration 里的值就是文件里的值
        // 如果不存在，Configuration 里的值就是我们在 PluginConfiguration.cs 中设置的默认值

        var assembly = Assembly.GetExecutingAssembly();
        AssemblyFilePath = assembly.Location;
    }

    /// <summary>
    /// 当 Emby 应用程序关闭时调用。
    /// </summary>
    public void OnApplicationShutdown()
    {
        // 在第一步中，我们暂时不执行任何操作
    }
}
