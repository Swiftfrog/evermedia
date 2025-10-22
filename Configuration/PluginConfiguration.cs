// Configuration/PluginConfiguration.cs
using MediaBrowser.Model.Plugins;
using System.ComponentModel;

namespace EverMedia.Configuration; // 使用命名空间组织代码

public class PluginConfiguration : BasePluginConfiguration
{
    public string BackupMode { get; set; } = "SideBySide";
    public string CentralizedRootPath { get; set; } = "";
    public bool EnableSelfHealing { get; set; } = true;
    public int MaxConcurrency { get; set; } = 4;
    public bool EnableOrphanCleanup { get; set; } = false;
    public string LogLevel { get; set; } = "Info";
}

