// ServerEntryPoint.cs
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using System.Threading.Tasks;

using EverMedia.Events;

namespace EverMedia;

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

    public void Run()
    {
        _libraryManager.ItemAdded += _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated += _eventListener.OnItemUpdated;
        _logger.Info("[EverMedia] Event handlers subscribed.");
    }

    public void Dispose()
    {
        _libraryManager.ItemAdded -= _eventListener.OnItemAdded;
        _libraryManager.ItemUpdated -= _eventListener.OnItemUpdated;
        _logger.Info("[EverMedia] Event handlers unsubscribed.");
    }
}
