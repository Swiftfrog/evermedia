// Plugin.cs
using MediaBrowser.Common.Configuration; // IConfigurationManager
using MediaBrowser.Common.Plugins; // IPlugin, BasePlugin
using MediaBrowser.Model.Plugins; // BasePluginConfiguration
using MediaBrowser.Model.Serialization; // IJsonSerializer
using System;// IApplicationHost 在 MediaBrowser.Common 命名空间下
using MediaBrowser.Common; // 引入你的提供者和任务命名空间
using EmbyMedia.Plugin.Providers;

namespace EmbyMedia.Plugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IPlugin
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
        }

        public override string Name => "EmbyMedia";

        public override Guid Id => Guid.Parse("35F12540-9EBD-9146-8E44-5D6D9BD66489"); // 请替换成你生成的全新 GUID

        public override string Description => "Creates .mediainfo backups for media items and restores them.";
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        // 可以在这里添加插件配置选项，例如是否处理 OriginalTitle 等
        // public bool ProcessOriginalTitle { get; set; } = false;
    }
}
