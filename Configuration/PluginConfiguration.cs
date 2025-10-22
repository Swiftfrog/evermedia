// Configuration/PluginConfiguration.cs
using MediaBrowser.Model.Plugins;
using System.ComponentModel;

namespace EverMedia.Configuration; // 使用命名空间组织代码

// 继承自 BasePluginConfiguration
public class PluginConfiguration : BasePluginConfiguration
{
    // 重写页面标题
    public override string EditorTitle => "我的插件选项";

    // 重写页面描述，支持换行符 \n
    public override string EditorDescription => "这是一个描述文本，显示在选项页面的顶部。\n下面的选项是创建 UI 元素的示例。";

    public string TargetFolder { get; set; }

    public bool IsDebugModeEnabled { get; set; }

    public enum MessageFormat
    {
        PlainText,
        Json,
        Xml
    }

    public MessageFormat OutputFormat { get; set; }

    public int ProcessingThreadCount { get; set; }

    public PluginConfiguration()
    {
        // 在构造函数中为属性设置默认值
        TargetFolder = "/path/to/default/folder";
        IsDebugModeEnabled = false;
        OutputFormat = MessageFormat.Json;
        ProcessingThreadCount = 4;
    }
}


