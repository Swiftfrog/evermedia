// EmbyMedia.Plugin.cs
using MediaBrowser.Common.Configuration; // IApplicationPaths
using MediaBrowser.Common.Plugins; // IPlugin, BasePlugin
using MediaBrowser.Model.Plugins; // BasePluginConfiguration
using MediaBrowser.Model.Serialization; // IXmlSerializer
using Microsoft.Extensions.DependencyInjection; // IServiceCollection
using System;

namespace EmbyMedia.Plugin;

// 1. 定义一个插件配置类，即使暂时不需要配置项
public class PluginConfiguration : BasePluginConfiguration
{
    // 例如，以后可以添加是否启用 STRM 探测等选项
    // public bool EnableStrmProbing { get; set; } = true;
}

// 2. 让 Plugin 继承 BasePlugin<PluginConfiguration>, 实现 IPlugin, 并实现 IPluginServiceRegistrator
public class Plugin : BasePlugin<PluginConfiguration>, IPlugin, IPluginServiceRegistrator // Implement IPluginServiceRegistrator
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
    }

    public override string Name => "EmbyMedia"; // 插件显示名称

    public override Guid Id => Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489"); // 您的唯一插件 GUID

    public override string Description => "Creates .mediainfo backups for media items and restores them.";

    // 3. 实现 IPluginServiceRegistrator 接口的 RegisterServices 方法
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        // 注册 ICustomMetadataProvider<Video> 实现
        serviceCollection.AddSingleton<ICustomMetadataProvider<Video>, MediaInfoCustomMetadataProvider>();
        // 如果有其他服务需要注册，也可以在这里添加
        // serviceCollection.AddSingleton<IMediaInfoService, MediaInfoService>();
    }
}
