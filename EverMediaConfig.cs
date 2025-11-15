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

    [DisplayName("集中存储路径")]
    [Description("当选择“集中存储”模式时，用于存放 .medinfo 文件的根文件夹路径。")]
    [EditFolderPicker]
    public string CentralizedRootPath { get; set; } = "";

    // [DisplayName("FFProbe 最大重试次数")]
    // [Description("当探测失败时，允许的最大自动重试次数。达到此限制后，插件将停止对该文件的自动探测，直到“失败重置时间”过去。")]
    // [MinValue(1), MaxValue(10)]
    // public int MaxProbeRetries { get; set; } = 3;
// 
    // [DisplayName("FFProbe 失败重置时间 (分钟)")]
    // [Description("当达到最大重试次数后，需要等待多久才能允许再次尝试。防止死循环，允许在一段时间后，自动重置。")]
    // [MinValue(1)]
    // public int ProbeFailureResetMinutes { get; set; } = 30;
// 
    // [DisplayName("计划任务-线程数量")]
    // [Description("同时发起操作的最多任务数。例如：2，同时触发2个.strm执行，适用于批量任务。")]
    // public int MaxConcurrency { get; set; } = 2;
// 
    // [DisplayName("计划任务- .strm 访问间隔（秒）")]
    // [Description(" .strm 调用 FFProbe 的最小间隔（秒），设为 0 表示禁用。例如：A.strm触发访问刷新media info后，B.strm需要2秒后才能继续访问，适用于批量任务。")]
    // [MinValue(0), MaxValue(60)]
    // public int BootstrapTaskRateLimitSeconds { get; set; } = 2;

    // [DisplayName("启用孤立文件清理");
    // [Description("清理不再有对应 .strm 文件的 .medinfo 文件。");
    // public bool EnableOrphanCleanup { get; set; } = false;

    [DisplayName("上次任务运行时间（UTC）")]
    [Description("MediaInfo 引导任务上次成功完成的 UTC 时间，用于增量扫描。请谨慎手动修改此值。")]
    public DateTime? LastBootstrapTaskRun { get; set; } = null; // 初始值为 null
    
    // --- 二级设置分组 1: 熔断策略 ---
    [DisplayName("高级设置：熔断策略")]
    [Description("配置针对 FFProbe 失败的重试与熔断机制，防止服务器因反复尝试损坏文件而过载。")]
    public ProbeFailureConfig FailureConfig { get; set; } = new ProbeFailureConfig();


    // --- 二级设置分组 2: 并发控制 ---
    [DisplayName("高级设置：计划任务并发")]
    [Description("配置计划任务（Bootstrap Task）的执行速率和并发度，避免批量处理时阻塞服务器。")]
    public ConcurrencyConfig TaskConfig { get; set; } = new ConcurrencyConfig();
    
}

public class ProbeFailureConfig : EditableOptionsBase
{
    [DisplayName("FFProbe 最大重试次数")]
    [Description("当探测失败时，允许的最大自动重试次数。达到此限制后，插件将停止对该文件的自动探测，直到“失败重置时间”过去。")]
    [MinValue(1), MaxValue(10)]
    public int MaxProbeRetries { get; set; } = 3;

    [DisplayName("FFProbe 失败重置时间 (分钟)")]
    [Description("当达到最大重试次数后，需要等待多久才能允许再次尝试。防止死循环，允许在一段时间后，自动重置。")]
    [MinValue(1)]
    public int ProbeFailureResetMinutes { get; set; } = 30;
}

public class ConcurrencyConfig : EditableOptionsBase
{
    [DisplayName("计划任务-线程数量")]
    [Description("同时发起操作的最多任务数。例如：2，同时触发2个.strm执行，适用于批量任务。")]
    public int MaxConcurrency { get; set; } = 2;

    [DisplayName("计划任务- .strm 访问间隔（秒）")]
    [Description(" .strm 调用 FFProbe 的最小间隔（秒），设为 0 表示禁用。例如：A.strm触发访问刷新media info后，B.strm需要2秒后才能继续访问，适用于批量任务。")]
    [MinValue(0), MaxValue(60)]
    public int BootstrapTaskRateLimitSeconds { get; set; } = 2;
}

public enum BackupMode { SideBySide, Centralized }