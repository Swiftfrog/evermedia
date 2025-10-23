// ServerEntryPoint.cs
using MediaBrowser.Controller.Library; // ILibraryManager
using MediaBrowser.Controller.Plugins; // IServerEntryPoint
using MediaBrowser.Model.Logging; // ILogger
using System.Threading.Tasks; // For async/await if needed in future
using EverMedia.Events; // 引入事件监听器

namespace EverMedia;

/// <summary>
/// 插件的服务器端入口点。
/// 负责在 Emby 启动时订阅事件，在关闭时取消订阅。
/// </summary>
public class ServerEntryPoint : IServerEntryPoint // ✅ 使用 IServerEntryPoint
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;
    private readonly MediaInfoEventListener _eventListener;

    // 构造函数：依赖注入 ILibraryManager, ILogger, 和 MediaInfoEventListener
    public ServerEntryPoint(
        ILibraryManager libraryManager,
        ILogger logger,
        MediaInfoEventListener eventListener // Emby DI 会自动创建并注入这个实例及其依赖 (MediaInfoService, ILogger)
    )
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _eventListener = eventListener;
    }

    /// <summary>
    /// 当服务器启动时调用。
    /// 订阅 ILibraryManager 的事件。
    /// </summary>
    public void Run() // ✅ 实现 IServerEntryPoint 的 Run 方法
    {
        _libraryManager.ItemAdded += _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated += _eventListener.OnItemUpdated;
        _logger.Info("[ServerEntryPoint] Event handlers subscribed.");
    }

    /// <summary>
    /// 当服务器关闭时调用。
    /// 取消订阅 ILibraryManager 的事件。
    /// </summary>
    public void Dispose() // ✅ 实现 IDisposable 的 Dispose 方法 (IServerEntryPoint 继承了 IDisposable)
    {
        _libraryManager.ItemAdded -= _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated -= _eventListener.OnItemUpdated;
        _logger.Info("[ServerEntryPoint] Event handlers unsubscribed.");
    }
}
