// Plugin.cs
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization; // 引入 IXmlSerializer
using MediaBrowser.Common.Configuration; // 引入 IApplicationPaths
using System;
using EverMedia.Configuration; // 引用您的配置类

namespace EverMedia;

/// <summary>
/// EverMedia 插件的主入口点。
/// </summary>
// ✅ 关键改动 1: 继承自泛型 BasePlugin<T>，并传入您的配置类作为类型参数。
// 这会告诉 Emby 使用 PluginConfiguration 类来自动生成 UI。
public class Plugin : BasePlugin<PluginConfiguration>
{
    // --- BasePlugin<T> 必须实现的属性 ---
    public override string Name => "EverMedia";
    public override string Description => "Self-healing MediaInfo persistence for.strm files.";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    // ✅ 关键改动 2: 添加一个符合 BasePlugin<T> 要求的构造函数。
    // Emby 会通过依赖注入传入所需的 IApplicationPaths 和 IXmlSerializer 实例。
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    // --- 插件实例 ---
    public static Plugin Instance { get; private set; }

    // ℹ️ 注意：以下内容已被简化或移除，因为 BasePlugin<T> 基类会处理它们：
    // - 不再需要手动实现 Version, AssemblyFilePath, DataFolderPath 属性。
    // - 不再需要 GetPluginInfo() 方法。
    // - 不再需要 OnApplicationStartup(), OnApplicationShutdown(), OnUninstalling() 的基本实现。
    //   如果需要自定义逻辑，可以重写 (override) 这些方法。
    // - 不再需要手动声明一个 public Configuration 属性来暴露配置。
    //   可以通过基类的 `base.Configuration` 来访问已加载的配置实例。
}
