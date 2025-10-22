// 引入必要的命名空间
using Emby.Plugins;
using MediaBrowser.Controller.Plugins; // IServerApplicationHost 在这里
using MediaBrowser.Common.Plugins;
using System;
using EverMedia.Configuration;

// 注意：以下 using 语句不再需要，因为构造函数不再使用它们
// using MediaBrowser.Model.Serialization;
// using MediaBrowser.Common.Configuration;

namespace EverMedia;

public class Plugin : BasePluginSimpleUI<PluginConfiguration>
{
    public override string Name => "EverMedia";
    public override string Description => "Self-healing MediaInfo persistence for.strm files.";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    // ✅ 关键修订: 更改构造函数以匹配 BasePluginSimpleUI<T> 的要求。
    // 它不再需要 IApplicationPaths 和 IXmlSerializer，
    // 而是需要 IServerApplicationHost。
    public Plugin(IServerApplicationHost applicationHost)
        : base(applicationHost) // 将 applicationHost 传递给基类
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; }
}
