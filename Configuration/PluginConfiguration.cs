// Configuration/PluginConfiguration.cs
// 1. 移除旧的 using 语句
// using MediaBrowser.Model.Plugins; 

// 2. 引入新的、必需的 using 语句
using Emby.Web.GenericEdit;
using System.ComponentModel;

namespace EverMedia.Configuration;

// 3. ✅ 关键修订: 更改基类
// 将 BasePluginConfiguration 更改为 EditableOptionsBase
public class PluginConfiguration : EditableOptionsBase
{
    // 你的所有配置项属性保持不变
    public string BackupMode { get; set; } = "SideBySide";
    public string CentralizedRootPath { get; set; } = "";
    public bool EnableSelfHealing { get; set; } = true;
    public int MaxConcurrency { get; set; } = 4;
    public bool EnableOrphanCleanup { get; set; } = false;
    public string LogLevel { get; set; } = "Info";
}
