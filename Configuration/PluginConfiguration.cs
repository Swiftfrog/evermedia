// Configuration/PluginConfiguration.cs
using MediaBrowser.Model.Plugins; // 引入 Plugins 相关模型，需要 BasePluginConfiguration

namespace EverMedia.Configuration; // 使用命名空间组织代码

/// <summary>
/// 定义 EverMedia 插件的用户可配置选项。
/// </summary>
public class PluginConfiguration : BasePluginConfiguration // 继承基类
{
    // 定义一个配置项：备份模式
    // public 属性意味着它会被 Emby UI 自动识别并提供编辑界面
    public string BackupMode { get; set; } = "SideBySide"; // 默认值为 "SideBySide"

    // 定义一个配置项：中心化备份路径（当 BackupMode 为 Centralized 时使用）
    public string CentralizedRootPath { get; set; } = ""; // 默认为空字符串

    // 定义一个配置项：是否启用自愈功能
    public bool EnableSelfHealing { get; set; } = true; // 默认启用

    // 定义一个配置项：计划任务的最大并发数
    public int MaxConcurrency { get; set; } = 4; // 默认值为 4

    // 定义一个配置项：是否启用孤立备份文件清理
    public bool EnableOrphanCleanup { get; set; } = false; // 默认禁用

    // 定义一个配置项：日志级别
    public string LogLevel { get; set; } = "Info"; // 默认为 "Info"
}
