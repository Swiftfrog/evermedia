using MediaBrowser.Common.Plugins; // IPlugin 接口所在命名空间
using System.Reflection; // 用于获取插件 GUID

namespace EverMedia; // 使用你的插件命名空间

/// <summary>
/// EverMedia 插件的主入口点，实现 IPlugin 接口。
/// </summary>
public class Plugin : IPlugin // 实现 IPlugin 接口
{
    // 插件的唯一标识符，使用 GUID
    public Guid Id { get; } = new Guid("781A1B2C-3D4E-5F6A-7B8C-9D0E1F2A3B4C"); // 请替换为你的插件生成的唯一 GUID

    // 插件名称
    public string Name => "EverMedia";

    // 插件描述
    public string Description => "Persistent MediaInfo for .strm files with self-healing.";

    // 插件版本
    public Version Version { get; } = new Version(1, 0, 0, 0);

    // 插件程序集文件路径 (由 Emby 自动设置)
    public string AssemblyFilePath { get; set; } = string.Empty;

    // 插件数据文件夹路径 (由 Emby 自动设置)
    public string DataFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取插件信息 (可选实现，通常由框架处理)
    /// </summary>
    /// <returns>PluginInfo 实例</returns>
    public PluginInfo GetPluginInfo()
    {
        // 框架通常会基于属性自动生成 PluginInfo
        return new PluginInfo
        {
            Name = Name,
            Version = Version.ToString(),
            Id = Id.ToString() // 虽然 Id 是 Guid，但 PluginInfo.Id 通常是字符串
        };
    }

    /// <summary>
    /// 在应用程序启动时调用 (插件加载时)
    /// </summary>
    public void OnApplicationStartup()
    {
        // 暂时留空，后续在此初始化服务、订阅事件等
        // 例如：Console.WriteLine("EverMedia Plugin Loaded!");
    }

    /// <summary>
    /// 在应用程序关闭时调用 (插件卸载时)
    /// </summary>
    public void OnApplicationShutdown()
    {
        // 暂时留空，后续在此清理资源、取消订阅事件等
    }

    /// <summary>
    /// 在插件即将卸载时调用
    /// </summary>
    public void OnUninstalling()
    {
        // 暂时留空，后续可在此执行卸载前的清理工作
    }
}
