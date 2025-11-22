// EverMediaConfig.cs - EN
using MediaBrowser.Model.Attributes;
using Emby.Web.GenericEdit;
using System.ComponentModel;

namespace EverMedia;

public class EverMediaConfig : EditableOptionsBase
{
    public override string EditorTitle => "EverMedia Settings";

    [DisplayName("Enable Plugin")]
    [Description("Enable or disable the plugin's real-time monitoring of .strm file changes.")]
    public bool EnablePlugin { get; set; } = false; // Default disabled

    [DisplayName("Enable Bootstrap Task")]
    [Description("Enable or disable the scheduled task (scan and persist .strm file MediaInfo).")]
    public bool EnableBootstrapTask { get; set; } = false; // Default disabled

    [DisplayName("Backup Mode")]
    [Description("Choose .medinfo files are stored. SideBySide: Same path as .strm files. Centralized: In a specified path.")]
    public BackupMode BackupMode { get; set; } = BackupMode.SideBySide;

    [DisplayName("Centralized Storage Path")]
    [Description("When 'Centralized' mode, path for storing .medinfo files.")]
    [EditFolderPicker]
    public string CentralizedRootPath { get; set; } = "";

    [DisplayName("Last Task Run Time (UTC)")]
    [Description("The UTC time when the MediaInfo Bootstrap Task last completed successfully, used for incremental scanning. Modify this value with caution.")]
    public DateTime? LastBootstrapTaskRun { get; set; } = null; // Initial value is null
    
    // --- Secondary Settings Group 1: Circuit Breaker Policy ---
    [DisplayName("Advanced: Circuit Breaker Policy")]
    [Description("Configure retry and circuit breaker mechanisms for FFProbe failures to prevent server overload from repeatedly attempting corrupted files.")]
    public ProbeFailureConfig FailureConfig { get; set; } = new ProbeFailureConfig();


    // --- Secondary Settings Group 2: Concurrency Control ---
    [DisplayName("Advanced: Scheduled Task Concurrency")]
    [Description("Configure the execution rate and concurrency of the scheduled task (Bootstrap Task) to avoid blocking the server during batch processing.")]
    public ConcurrencyConfig TaskConfig { get; set; } = new ConcurrencyConfig();
    
}

public class ProbeFailureConfig : EditableOptionsBase
{
    public override string EditorTitle => "Probe Failure Settings";
    
    [DisplayName("FFProbe Max Retries")]
    [Description("The maximum number of automatic retries allowed when probing fails. After reaching this limit, the plugin will stop automatic probing of the file until the 'failure reset time' has passed.")]
    [MinValue(1), MaxValue(10)]
    public int MaxProbeRetries { get; set; } = 3;

    [DisplayName("FFProbe Failure Reset Time (Minutes)")]
    [Description("How long to wait before allowing retry attempts after reaching the maximum retry count. Prevents infinite loops while allowing automatic reset after a period of time.")]
    [MinValue(1)]
    public int ProbeFailureResetMinutes { get; set; } = 30;
}

public class ConcurrencyConfig : EditableOptionsBase
{
    public override string EditorTitle => "Concurrency Settings";
    
    [DisplayName("Scheduled Task - Thread Count")]
    [Description("Maximum number of concurrent operations. For example: 2 means triggering 2 .strm executions simultaneously, suitable for batch tasks.")]
    public int MaxConcurrency { get; set; } = 2;

    [DisplayName("Scheduled Task - .strm Access Interval (Seconds)")]
    [Description("Minimum interval (in seconds) between .strm FFProbe calls. Set to 0 to disable. For example: After A.strm triggers a media info refresh, B.strm must wait 2 seconds before accessing, suitable for batch tasks.")]
    [MinValue(0), MaxValue(60)]
    public int BootstrapTaskRateLimitSeconds { get; set; } = 2;
}

public enum BackupMode { SideBySide, Centralized }