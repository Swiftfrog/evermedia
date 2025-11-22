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
    public override string Description => "Managing STRM files: Persistence, backup, and repair";
    public override Guid Id => new Guid("7B921178-7C5B-42D6-BB7C-42E8B00C2C7D");

    public EverMediaConfig Configuration => GetOptions();

    public void UpdateLastBootstrapTaskRun(DateTime? newTimestamp)
    {
        var config = GetOptions();
        if (config != null)
        {
            config.LastBootstrapTaskRun = newTimestamp;
            SaveOptions(config);
        }
    }

    public Plugin(IServerApplicationHost applicationHost)
        : base(applicationHost)
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; } = null!;
    
    public Stream GetThumbImage()
    {
        var assembly = GetType().Assembly;
        const string resourceName = "EverMedia.EverMediaLogo.png";
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Failed to load embedded logo resource: '{resourceName}'. " +
                "Check that the file is included as <EmbeddedResource> in EverMedia.csproj.");
        }
        
        return stream;
    }
    public ImageFormat ThumbImageFormat => ImageFormat.Png;
    
}