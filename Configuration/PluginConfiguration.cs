using Emby.Web.GenericEdit;
using System.ComponentModel;

namespace EverMedia.Configuration;

public class PluginConfiguration : EditableOptionsBase
{
    // ✅ 关键修订: 实现继承自 EditableOptionsBase 的抽象成员。
    // 这个属性将作为你插件配置页面的主标题。
    public override string EditorTitle => "EverMedia Settings";

    // --- 你现有的配置项保持不变 ---
    public string BackupMode { get; set; } = "SideBySide";
    public string CentralizedRootPath { get; set; } = "";
    public bool EnableSelfHealing { get; set; } = true;
    public int MaxConcurrency { get; set; } = 4;
    public bool EnableOrphanCleanup { get; set; } = false;
    public string LogLevel { get; set; } = "Info";
}
