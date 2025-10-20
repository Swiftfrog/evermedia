kusing MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using System;

namespace EmbyMedia.Plugin;

public class Plugin : BasePlugin // 继承 BasePlugin
{
    public override string Name => "EmbyMedia"; // 插件显示名称

    public override Guid Id => Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489"); // 您的唯一插件 GUID

    // 使用 BasePlugin 的无参数构造函数
    public Plugin() : base() // 调用父类的无参数构造函数
    {
        // 如果需要 IApplicationPaths 或 IXmlSerializer，通常它们是通过依赖注入在其他地方（如 Provider 或 Task 的构造函数）获取的，
        // 或者可以通过 Plugin.Instance.ApplicationPaths 等方式在运行时获取（如果 BasePlugin 提供了这样的属性）。
        // BasePlugin 本身通常不直接在构造函数中接收这些服务。
    }
}
