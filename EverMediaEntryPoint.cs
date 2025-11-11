// ServerEntryPoint.cs
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using System.Threading.Tasks;

using EverMedia.Events;

namespace EverMedia;

/// 负责在 Emby 启动时订阅事件，在关闭时取消订阅。
public class EverMediaEntryPoint : IServerEntryPoint
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;
    private readonly EverMediaEventListener _eventListener;

    public EverMediaEntryPoint(
        ILibraryManager libraryManager,
        ILogger logger,
        EverMediaEventListener eventListener
    )
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _eventListener = eventListener;
    }

    /// 当服务器启动时调用。
    /// 订阅 ILibraryManager 的事件。
    public void Run() // ✅ 实现 IServerEntryPoint 的 Run 方法
    {
        _libraryManager.ItemAdded += _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated += _eventListener.OnItemUpdated;
        _logger.Info("[EverMedia] Event handlers subscribed.");
    }

    /// 当服务器关闭时调用。
    /// 取消订阅 ILibraryManager 的事件。
    public void Dispose()
    {
        _libraryManager.ItemAdded -= _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated -= _eventListener.OnItemUpdated;
        _logger.Info("[EverMedia] Event handlers unsubscribed.");
    }
}
