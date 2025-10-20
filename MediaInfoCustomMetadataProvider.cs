using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library; // For ILibraryManager if needed in provider
using MediaBrowser.Controller.Providers; // ICustomMetadataProvider, IHasOrder, MetadataRefreshOptions
using MediaBrowser.Model.Entities; // BaseItem, Video, MetadataResult, ItemUpdateType
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

    // Emby 的 ICustomMetadataProvider<Video>.FetchAsync 方法签名
    // 注意：这个签名可能也随 Emby 版本变化，需要确认 4.9.1.80 的确切签名
    // 根据之前的错误信息，可能是缺少 LibraryOptions 参数，或者参数顺序不同
    // 通常签名是: Task<MetadataResult<T>> FetchAsync(T item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    // 但有时会包含 LibraryOptions
    public async Task<MetadataResult<Video>> FetchAsync(Video item, MetadataRefreshOptions options, LibraryOptions libraryOptions, CancellationToken cancellationToken) // Corrected signature
    {
        var result = new MetadataResult<Video> { Item = item, QueriedById = false, HasMetadataChanged = false };

        _logger.Debug("Processing item {0} in {1}", item.Path, Name); // 使用 Emby ILogger 的 Debug 方法

        // Check if it's a STRM file and needs probing
        if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("Processing STRM file {0} in {1}", item.Path, Name); // 使用 Emby ILogger 的 Debug 方法
            // var probeResult = await _mediaInfoService.EnsureStrmMediaInfoAsync(item, cancellationToken);
            // For now, assume a placeholder or direct implementation
            var probeResult = await EnsureStrmMediaInfoAsync(item, cancellationToken); // Placeholder call
            if (probeResult)
            {
                result.HasMetadataChanged = true; // Indicate that MediaInfo might have changed
                _logger.Debug("STRM file {0} MediaInfo updated, triggering backup.", item.Path); // 使用 Emby ILogger 的 Debug 方法
            }
        }

        // Always attempt to backup the current MediaInfo (whether just probed or already existed)
        // var backupResult = await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken);
        var backupResult = await BackupMediaInfoAsync(item, cancellationToken); // Placeholder call
        if (backupResult)
        {
            result.HasMetadataChanged = true; // Indicate that backup was created/updated
            _logger.Debug("MediaInfo backup completed for {0}", item.Path); // 使用 Emby ILogger 的 Debug 方法
        }

        return result;
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
