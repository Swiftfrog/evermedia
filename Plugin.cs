// Plugin.cs
using MediaBrowser.Controller.Plugins;
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
// ✅ 关键修订: 将基类从 BasePlugin<T> 更改为 BasePluginSimpleUI<T>。
// 这会激活 Emby 的声明式 UI 引擎，根据你的 PluginConfiguration 类自动生成配置页面。
public class Plugin : BasePluginSimpleUI<PluginConfiguration>
///public class Plugin : BasePlugin<PluginConfiguration>
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

}
