// 必须添加此命名空间引用
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using System.Reflection;

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

    // IPlugin.Id 要求返回 Guid 类型
    // 使用一个固定的 Guid，确保插件的唯一性
    public Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D"); // 替换为你自己的唯一 Guid

    // IPlugin.Version 要求返回 System.Version 类型
    public Version Version => new Version(1, 0, 0, 0);

    // IPlugin.AssemblyFilePath 需要外部注入或在运行时获取
    public string AssemblyFilePath { get; private set; } = string.Empty;

    // IPlugin.DataFolderPath 需要外部注入或在运行时获取
    public string DataFolderPath { get; private set; } = string.Empty;

    // --- IPlugin 接口必须实现的方法 ---

    // IPlugin.GetPluginInfo() 用于提供插件信息
    public PluginInfo GetPluginInfo()
    {
        return new PluginInfo
        {
            Name = this.Name,
            Description = this.Description,
            // Id 通常由 IPlugin.Id 提供，但 GetPluginInfo 也可以包含
            Id = this.Id,
            Version = this.Version.ToString()
        };
    }

    // IPlugin.OnUninstalling() 在插件卸载前调用
    public void OnUninstalling()
    {
        // 在第一步中，我们暂时不执行任何操作
        // 后续步骤可能需要在此处执行清理逻辑
    }

    // --- 插件生命周期方法 (非 IPlugin 接口成员，但常用) ---

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

        // 获取 AssemblyFilePath 和 DataFolderPath (通常由 Emby 在加载时设置)
        // 这里使用 Assembly 信息作为示例，实际路径由 Emby 框架提供
        var assembly = Assembly.GetExecutingAssembly();
        AssemblyFilePath = assembly.Location;
        // DataFolderPath 通常由 Emby 通过依赖注入等方式设置，这里暂时留空或设为默认值
        // 实际上，Emby 会自动设置这些路径，我们只需要实现接口
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
