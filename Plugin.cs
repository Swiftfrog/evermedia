using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace EmbyMedia.Plugin;

public class Plugin : BasePlugin
{
    public override string Name => "EmbyMedia"; // 插件显示名称

    public override Guid Id => Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489"); // 生成并替换

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
    }
}
