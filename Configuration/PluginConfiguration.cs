// Configuration/PluginConfiguration.cs
using MediaBrowser.Model.Attributes;
using Emby.Web.GenericEdit; // 包含 EditableOptionsBase
using System.ComponentModel; // 可选，用于 DisplayName 和 Description

namespace EverMedia.Configuration;

public class PluginConfiguration : EditableOptionsBase // ✅ 继承 EditableOptionsBase
{
    // ✅ 实现 EditorTitle，作为 UI 上的主标题
    public override string EditorTitle => "EverMedia Settings";

    // --- 配置项 ---
    // 可选：使用 DisplayName 和 Description 为 UI 提供更好的标签和提示
    public enum BackupMode { SideBySide, Centralized }
    public IList<BackupMode> RestrictedCodecList => new { BackupMode.SideBySide, BackupMode.Centralized };
    [DisplayName("Backup Mode")]
    [Description("Choose how to store .medinfo files. SideBySide: Next to the .strm file. Centralized: In a single specified root folder.")]
    // public string BackupMode { get; set; } = "SideBySide";
    //public IList<BackupMode> RestrictedCodecList => new[] { BackupMode.SideBySide, BackupMode.Centralized };
    [SelectItemsSource(nameof(RestrictedCodecList))]
    public BackupMode RestrictedEnumSelect { get; set; } = BackupMode.SideBySide;


    [DisplayName("Centralized Root Path")]
    [Description("Root folder path for storing .medinfo files when 'Centralized' mode is selected.")]
    // public string CentralizedRootPath { get; set; } = "";
    [EditFolderPicker]
    public string CentralizedRootPath { get; set; } = "";


    [DisplayName("Enable Self-Healing")]
    [Description("Automatically restore MediaInfo if it gets lost or cleared (e.g., after a metadata refresh).")]
    public bool EnableSelfHealing { get; set; } = true;

    [DisplayName("Max Concurrency")]
    [Description("Maximum number of concurrent operations for the bootstrap task.")]
    public int MaxConcurrency { get; set; } = 4;

    [DisplayName("Enable Orphan Cleanup")]
    [Description("Clean up .medinfo files that no longer have a corresponding .strm file.")]
    public bool EnableOrphanCleanup { get; set; } = false;

    [DisplayName("Log Level")]
    [Description("Minimum level for logging messages from this plugin.")]
    public string LogLevel { get; set; } = "Info";
}
