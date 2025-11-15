// EverMediaConfig.cs
using MediaBrowser.Model.Attributes;
using Emby.Web.GenericEdit;
using System.ComponentModel;

namespace EverMedia;

public class EverMediaConfig : EditableOptionsBase
{
    public override string EditorTitle => "EverMedia Settings";

    [DisplayName("启用插件")]
    [Description("启用或禁用插件的核心功能（实时监听 .strm 文件变化）。")]
    public bool EnablePlugin { get; set; } = true; // 默认启用

    [DisplayName("启用引导任务")]
    [Description("启用或禁用计划任务（扫描并持久化 .strm 文件的 MediaInfo）。")]
    public bool EnableBootstrapTask { get; set; } = false; // 默认启用

    [DisplayName("备份模式")]
    [Description("选择 .medinfo 文件的存储方式。SideBySide: 和.strm 文件放在同一目录下。Centralized: 存放在指定的目录中。")]
    public BackupMode BackupMode { get; set; } = BackupMode.SideBySide;

    [DisplayName("集中存储根路径")]
    [Description("当选择“集中存储”模式时，用于存放 .medinfo 文件的根文件夹路径。")]
    [EditFolderPicker]
    public string CentralizedRootPath { get; set; } = "";

    [DisplayName("启用自动修复")]
    [Description("当 MediaInfo 信息丢失或被清除时（例如在元数据刷新后），自动恢复。")]
    public bool EnableSelfHealing { get; set; } = true;

    [DisplayName("FFProbe 最大重试次数")]
    [Description("当探测失败时，允许的最大自动重试次数。达到此限制后，插件将停止对该文件的自动探测，直到“失败重置时间”过去。")]
    [MinValue(1), MaxValue(10)]
    public int MaxProbeRetries { get; set; } = 3;

    [DisplayName("FFProbe 失败重置时间 (分钟)")]
    [Description("当达到最大重试次数后，需要等待多久才能允许再次尝试。这防止了死循环，同时也允许在一段时间后（例如你修复文件并手动刷新后）自动重置熔断器。")]
    [MinValue(1)]
    public int ProbeFailureResetMinutes { get; set; } = 30;

    [DisplayName("最大并发数")]
    [Description("引导任务允许的最大并发操作数量。")]
    public int MaxConcurrency { get; set; } = 2;

    [DisplayName("引导任务调用间隔（秒）")]
    [Description("引导任务中两次 FFProbe 调用之间的最小间隔（秒），用于避免对 HTTP 服务器造成过大压力。设为 0 表示禁用限流。")]
    [MinValue(0), MaxValue(60)]
    public int BootstrapTaskRateLimitSeconds { get; set; } = 2;

    // [DisplayName("启用孤立文件清理");
    // [Description("清理不再有对应 .strm 文件的 .medinfo 文件。");
    // public bool EnableOrphanCleanup { get; set; } = false;

    [DisplayName("上次引导任务运行时间（UTC）")]
    [Description("MediaInfo 引导任务上次成功完成的 UTC 时间，用于增量扫描。请谨慎手动修改此值。")]
    public DateTime? LastBootstrapTaskRun { get; set; } = null; // 初始值为 null
}

public enum BackupMode { SideBySide, Centralized }