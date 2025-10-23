// ServerEntryPoint.cs
using MediaBrowser.Controller.Library; // ILibraryManager
using MediaBrowser.Controller.Plugins; // IPlugin
using MediaBrowser.Model.Logging; // ILogger
using System;
using EverMedia.Events; // 引入事件监听器

namespace EverMedia;

/// <summary>
/// 插件的服务器端入口点。
/// 负责在 Emby 启动时订阅事件，在关闭时取消订阅。
/// </summary>
public class ServerEntryPoint : IApplicationEntryPoint // 使用 IApplicationEntryPoint 是常见的做法
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
    /// 当应用程序启动时调用。
    /// 订阅 ILibraryManager 的事件。
    /// </summary>
    public void Run()
    {
        _libraryManager.ItemAdded += _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated += _eventListener.OnItemUpdated;
        _logger.Info("[ServerEntryPoint] Event handlers subscribed.");
    }

    /// <summary>
    /// 当应用程序关闭时调用。
    /// 取消订阅 ILibraryManager 的事件。
    /// </summary>
    public void Dispose() // IApplicationEntryPoint 通常实现 IDisposable 用于清理
    {
        _libraryManager.ItemAdded -= _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated -= _eventListener.OnItemUpdated;
        _logger.Info("[ServerEntryPoint] Event handlers unsubscribed.");
    }
}
