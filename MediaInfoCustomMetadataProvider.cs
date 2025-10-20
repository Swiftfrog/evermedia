using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

public class MediaInfoCustomMetadataProvider : ICustomMetadataProvider<Video>, IHasOrder
{
    private readonly IMediaInfoService _mediaInfoService;
    private readonly ILogger<MediaInfoCustomMetadataProvider> _logger;

    public MediaInfoCustomMetadataProvider(IMediaInfoService mediaInfoService, ILogger<MediaInfoCustomMetadataProvider> logger)
    {
        _mediaInfoService = mediaInfoService;
        _logger = logger;
    }

    public string Name => "EmbyMedia Metadata Provider";

    // Run early in the custom provider chain, but after main providers
    public int Order => 0;

    public async Task<MetadataResult<Video>> FetchAsync(Video item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Video> { Item = item, QueriedById = false, HasMetadataChanged = false };

        // Check if it's a STRM file and needs probing
        if (item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Processing STRM file {ItemPath} in {ProviderName}", item.Path, Name);
            var probeResult = await _mediaInfoService.EnsureStrmMediaInfoAsync(item, cancellationToken);
            if (probeResult)
            {
                result.HasMetadataChanged = true; // Indicate that MediaInfo might have changed
                _logger.LogDebug("STRM file {ItemPath} MediaInfo updated, triggering backup.", item.Path);
            }
        }

        // Always attempt to backup the current MediaInfo (whether just probed or already existed)
        var backupResult = await _mediaInfoService.BackupMediaInfoAsync(item, cancellationToken);
        if (backupResult)
        {
            result.HasMetadataChanged = true; // Indicate that backup was created/updated
            _logger.LogDebug("MediaInfo backup completed for {ItemPath}", item.Path);
        }

        return result;
    }
}
