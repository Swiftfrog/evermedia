// Plugin.cs
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;

using EverMedia.Tasks;

namespace EverMedia;

public class Plugin : BasePluginSimpleUI<EverMediaConfig>, IHasThumbImage
{
    public override string Name => "EverMedia";
    public override string Description => "Self-healing MediaInfo persistence for .strm files.";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    // 添加一个公共属性来安全地暴露配置。
    // 这个属性可以从插件内部调用受保护的 GetOptions() 方法。
    public EverMediaConfig Configuration => GetOptions();

    // 添加一个公共方法来更新并保存 LastBootstrapTaskRun 时间戳
    public void UpdateLastBootstrapTaskRun(DateTime? newTimestamp)
    {
        var config = GetOptions();
        if (config != null)
        {
            config.LastBootstrapTaskRun = newTimestamp;
            SaveOptions(config);
        }
    }

    // 构造函数使用 IServerApplicationHost
    public Plugin(IServerApplicationHost applicationHost)
        : base(applicationHost)
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; } = null!; // 初始化为 null! 以避免未赋值警告
    
    public Stream GetThumbImage()
    {
        var assembly = GetType().Assembly;
        const string resourceName = "EverMedia.EverMediaLogo.webp";
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Failed to load embedded logo resource: '{resourceName}'. " +
                "Check that the file is included as <EmbeddedResource> in EverMedia.csproj.");
        }
        
        return stream;
    }
    public ImageFormat ThumbImageFormat => ImageFormat.Webp;
    
}