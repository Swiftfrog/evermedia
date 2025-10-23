// Plugin.cs
using MediaBrowser.Controller; // IServerApplicationHost
using MediaBrowser.Controller.Plugins; // BasePluginSimpleUI
using MediaBrowser.Common.Plugins; // 包含 IPlugin 相关接口
using System;
using System.Collections.Generic; // For IEnumerable
using EverMedia.Configuration;
using EverMedia.Tasks; // 引入计划任务

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

    // --- 新增：实现 IPlugin.GetTasks 以注册计划任务 ---
    public IEnumerable<IScheduledTask> GetTasks() // 返回插件提供的计划任务列表
    {
        // 返回一个包含 MediaInfoBootstrapTask 实例的集合
        // Emby 框架会自动发现并注册这些任务
        yield return new MediaInfoBootstrapTask(); // 这里需要无参构造函数或依赖注入
    }

}
