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

    // 修正后的 FetchAsync 方法签名，完全匹配 ICustomMetadataProvider<Video> 接口
    public async Task<ItemUpdateType> FetchAsync(
        MetadataResult<Video> itemResult, // 第一个参数是 MetadataResult<Video>
        MetadataRefreshOptions options,
        LibraryOptions libraryOptions, // 包含 LibraryOptions
        CancellationToken cancellationToken)
    {
        // 从 itemResult 中获取实际的 Video 对象
        var item = itemResult.Item;

        _logger.Debug("Processing item {0} in {1}", item.Path, Name);

        // Check if it's a STRM file and needs probing
        if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("Processing STRM file {0} in {1}", item.Path, Name);
            // var probeResult = await _mediaInfoService.EnsureStrmMediaInfoAsync(item, cancellationToken);
            // For now, assume a placeholder or direct implementation
            var probeResult = await EnsureStrmMediaInfoAsync(item, cancellationToken); // Placeholder call
            if (probeResult)
            {
                // result.HasMetadataChanged = true; // itemResult 没有这个属性，而是通过返回值告知
                _logger.Debug("STRM file {0} MediaInfo updated, triggering backup.", item.Path);
            }
        }

        // Always attempt to backup the current MediaInfo (whether just probed or already existed)
        // var backupResult = await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken);
        var backupResult = await BackupMediaInfoAsync(item, cancellationToken); // Placeholder call
        if (backupResult)
        {
            // result.HasMetadataChanged = true; // 同上
            _logger.Debug("MediaInfo backup completed for {0}", item.Path);
        }

        // 返回 ItemUpdateType 来告知 Emby 是否发生了元数据更改
        // 如果 STRM 探测或备份导致了 MediaSourceInfo 的变化，可能需要返回 MetadataEdit 或其他相关类型
        // 如果没有实际更改，则返回 None
        // 这里需要根据 EnsureStrmMediaInfoAsync 和 BackupMediaInfoAsync 的结果来判断
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
