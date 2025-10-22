// Plugin.cs
using MediaBrowser.Controller; // IServerApplicationHost
using MediaBrowser.Controller.Plugins; // BasePluginSimpleUI
using MediaBrowser.Common.Plugins; // 包含 IPlugin 相关接口
using System;
using EverMedia.Configuration;

namespace EverMedia;

public class Plugin : BasePluginSimpleUI<PluginConfiguration> // ✅ 继承 BasePluginSimpleUI
{
    public override string Name => "EverMedia";
    public override string Description => "Self-healing MediaInfo persistence for .strm files.";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    // ✅ 关键修订: 添加一个公共属性来安全地暴露配置。
    // 这个属性可以从插件内部调用受保护的 GetOptions() 方法。
    public PluginConfiguration Configuration => GetOptions();

    // ✅ 构造函数使用 IServerApplicationHost
    public Plugin(IServerApplicationHost applicationHost)
        : base(applicationHost) // 将 applicationHost 传递给基类
    {
        Instance = this; // 在构造函数中设置 Instance
    }

    // ✅ 静态实例
    public static Plugin Instance { get; private set; } = null!; // 初始化为 null! 以避免未赋值警告

    // BasePluginSimpleUI 已处理 Configuration、GetPluginInfo、OnUninstalling 等
    // 以及 UI 生成所需的信息。


    // OnApplicationStartup 和 OnApplicationShutdown 通常在这里定义
    // 用于插件初始化和清理，但也可以留空或根据需要实现。
    public void OnApplicationStartup()
    {
        // 在这里进行插件启动时的初始化工作
        // Configuration 已由基类加载
    }

    public void OnApplicationShutdown()
    {
        // 在这里进行插件关闭时的清理工作
    }
}
