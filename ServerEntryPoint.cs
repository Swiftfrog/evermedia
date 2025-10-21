using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using System.Threading.Tasks;

// 2. ServerEntryPoint.cs - 最简事件订阅
public class ServerEntryPoint : IServerEntryPoint
{
    private readonly ILibraryManager _libraryManager;
    
    public ServerEntryPoint(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }
    
    public void Run()
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
    }
    
    private async void OnItemAdded(object sender, ItemChangeEventArgs e)
    {
        await HandleStrmItemAsync(e.Item);
    }
    
    private async void OnItemUpdated(object sender, ItemChangeEventArgs e)
    {
        await HandleStrmItemAsync(e.Item);
    }
    
    private async Task HandleStrmItemAsync(BaseItem item)
    {
        if (item == null || !item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) ?? false)
            return;
            
        // 暂时只记录日志，验证事件是否触发
        var logger = ServiceContainer.Resolve<ILogManager>().GetLogger("evermedia");
        logger.Info($"Detected .strm file: {item.Path}");
    }
    
    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
    }
}
