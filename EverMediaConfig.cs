// Configuration/PluginConfiguration.cs
using MediaBrowser.Model.Attributes;
using Emby.Web.GenericEdit; // 包含 EditableOptionsBase
using System.ComponentModel; // 可选，用于 DisplayName 和 Description

namespace EverMedia;
//public class PluginConfiguration : EditableOptionsBase // ✅ 继承 EditableOptionsBase
public class EverMediaConfig : EditableOptionsBase // ✅ 继承 EditableOptionsBase
{
    // ✅ 实现 EditorTitle，作为 UI 上的主标题
    public override string EditorTitle => "EverMedia Settings";

    [DisplayName("Backup Mode")]
    [Description("Choose how to store .medinfo files. SideBySide: Next to the .strm file. Centralized: In a single specified root folder.")]
    public BackupMode BackupMode { get; set; } = BackupMode.SideBySide;

    [DisplayName("Centralized Root Path")]
    [Description("Root folder path for storing .medinfo files when 'Centralized' mode is selected.")]
    [EditFolderPicker]
    public string CentralizedRootPath { get; set; } = "";


    [DisplayName("Enable Self-Healing")]
    [Description("Automatically restore MediaInfo if it gets lost or cleared (e.g., after a metadata refresh).")]
    public bool EnableSelfHealing { get; set; } = true;

    [DisplayName("Max Concurrency")]
    [Description("Maximum number of concurrent operations for the bootstrap task.")]
    public int MaxConcurrency { get; set; } = 2;

    [DisplayName("Bootstrap Task Rate Limit (Seconds)")]
    [Description("Minimum interval in seconds between FFProbe calls during the bootstrap task to avoid overwhelming the HTTP server. Set to 0 to disable rate limiting.")] // 提供描述
    [MinValue(0), MaxValue(10)]
    public int BootstrapTaskRateLimitSeconds { get; set; } = 2;

    [DisplayName("Enable Orphan Cleanup")]
    [Description("Clean up .medinfo files that no longer have a corresponding .strm file.")]
    public bool EnableOrphanCleanup { get; set; } = false;

    [DisplayName("Last Bootstrap Task Run (UTC)")]
    [Description("The UTC time when the MediaInfo Bootstrap Task last completed successfully. Used for incremental scanning. Modify manually with caution.")] // 提供描述，告知用户其用途和修改注意事项
    public DateTime? LastBootstrapTaskRun { get; set; } = null; // 初始值为 null

    [DisplayName("Log Level")]
    [Description("Minimum level for logging messages from this plugin.")]
    //public string LogLevel { get; set; } = "Info";
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

}

public enum BackupMode { SideBySide, Centralized }
public enum LogLevel { Debug, Info, Warn, Error }