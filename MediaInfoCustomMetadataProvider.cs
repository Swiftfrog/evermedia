using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library; // For ILibraryManager if needed in provider
using MediaBrowser.Controller.Providers; // ICustomMetadataProvider, IHasOrder, MetadataRefreshOptions
using MediaBrowser.Model.Configuration; // LibraryOptions
using MediaBrowser.Model.Entities; // BaseItem, Video, MetadataResult, ItemUpdateType, MediaProtocol, SubtitleDeliveryMethod
using MediaBrowser.Model.Logging; // ILogger
using MediaBrowser.Model.Providers; // MetadataResult<T>
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

public class MediaInfoCustomMetadataProvider : ICustomMetadataProvider<Video>, IHasOrder // Note: Video type
{
    private readonly ILibraryManager _libraryManager; // If needed for GetLibraryOptions
    private readonly ILogger _logger; // 使用 Emby 的非泛型 ILogger

    public MediaInfoCustomMetadataProvider(ILibraryManager libraryManager, ILogger logger) // 依赖注入 Emby 的 ILogger
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "EmbyMedia Metadata Provider";

    // Run early in the custom provider chain, but after main providers
    public int Order => 0;

    public async Task<ItemUpdateType> FetchAsync(
        MetadataResult<Video> itemResult, // 第一个参数是 MetadataResult<Video>
        MetadataRefreshOptions options,
        LibraryOptions libraryOptions, // 包含 LibraryOptions
        CancellationToken cancellationToken)
    {
        // 从 itemResult 中获取实际的 Video 对象
        var item = itemResult.Item;

        _logger.Debug("Processing item {0} in {1}", item.Path, Name);

        // 声明 probeResult 变量，初始值为 false
        bool probeResult = false;

        // Check if it's a STRM file and needs probing
        if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("Processing STRM file {0} in {1}", item.Path, Name);
            // For now, assume a placeholder or direct implementation
            probeResult = await EnsureStrmMediaInfoAsync(item, cancellationToken); // 在 if 块内为其赋值
            if (probeResult)
            {
                _logger.Debug("STRM file {0} MediaInfo updated, triggering backup.", item.Path);
            }
        }

        // Always attempt to backup the current MediaInfo (whether just probed or already existed)
        var backupResult = await BackupMediaInfoAsync(item, cancellationToken); // Placeholder call
        if (backupResult)
        {
            _logger.Debug("MediaInfo backup completed for {0}", item.Path);
        }

        // 现在 probeResult 在 if (probeResult || backupResult) 的作用域内了
        if (probeResult || backupResult)
        {
             return ItemUpdateType.MetadataEdit; // 假设探测或备份意味着元数据有变化
        }
        return ItemUpdateType.None; // 默认无变化
    }

    // Placeholder methods - implement the actual logic here or via injected service
    private async Task<bool> EnsureStrmMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        _logger.Info("Probing STRM file {0} - Placeholder", item.Path);
        // ... actual probe code using IMediaSourceManager ...
        return true; // Placeholder return
    }

    private async Task<bool> BackupMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        _logger.Info("Backing up MediaInfo for {0} - Placeholder", item.Path);
        // ... actual backup code ...
        return true; // Placeholder return
    }
}
