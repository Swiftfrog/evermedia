// Configuration/PluginConfiguration.cs
using Emby.Web.GenericEdit; // 包含 EditableOptionsBase
using System.ComponentModel; // 用于 DisplayName 和 Description
using MediaBrowser.Model.Plugins; // 包含 BasePluginConfiguration (虽然我们继承了 EditableOptionsBase)

namespace EverMedia.Configuration;

public class PluginConfiguration : EditableOptionsBase // ✅ 继承 EditableOptionsBase
{
    // ✅ 实现 EditorTitle，作为 UI 上的主标题
    public override string EditorTitle => "EverMedia Settings";

    // --- 配置项：备份路径策略 ---
    [DisplayName("Backup Path Strategy")]
    [Description("Choose how to store .medinfo files. SideBySide: Next to the .strm file. Centralized: In a single specified root folder.")]
    [Option("SideBySide")] // 定义下拉选项
    [Option("Centralized")]
    public string BackupMode { get; set; } = "SideBySide"; // 默认值

    // --- 配置项：中心化备份路径（当 BackupMode 为 Centralized 时使用） ---
    [DisplayName("Centralized Root Path")]
    [Description("Root folder path for storing .medinfo files when 'Centralized' mode is selected.")]
    [VisibleCondition("BackupMode", "Centralized")] // 当 BackupMode 为 "Centralized" 时才显示此选项
    public string CentralizedRootPath { get; set; } = "";

    // --- 配置项：是否启用自愈功能 ---
    [DisplayName("Enable Self-Healing")]
    [Description("Automatically restore MediaInfo if it gets lost or cleared (e.g., after a metadata refresh).")]
    public bool EnableSelfHealing { get; set; } = true; // 默认启用

    // --- 配置项：计划任务的最大并发数 ---
    [DisplayName("Max Concurrency")]
    [Description("Maximum number of concurrent operations for the bootstrap task.")]
    [Range(1, 10)] // 限制范围，例如 1 到 10
    public int MaxConcurrency { get; set; } = 4; // 默认值为 4

    // --- 配置项：是否启用孤立备份文件清理 ---
    [DisplayName("Enable Orphan Cleanup")]
    [Description("Clean up .medinfo files that no longer have a corresponding .strm file.")]
    public bool EnableOrphanCleanup { get; set; } = false; // 默认禁用

    // --- 配置项：日志级别 ---
    [DisplayName("Log Level")]
    [Description("Minimum level for logging messages from this plugin.")]
    [Option("Debug")] // 定义下拉选项
    [Option("Info")]
    [Option("Warn")]
    [Option("Error")]
    public string LogLevel { get; set; } = "Info"; // 默认为 "Info"
}
