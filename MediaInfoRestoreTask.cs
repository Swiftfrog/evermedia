using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

public class MediaInfoRestoreTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaInfoService _mediaInfoService;
    private readonly ILogger<MediaInfoRestoreTask> _logger;

    public MediaInfoRestoreTask(ILibraryManager libraryManager, IMediaInfoService mediaInfoService, ILogger<MediaInfoRestoreTask> logger)
    {
        _libraryManager = libraryManager;
        _mediaInfoService = mediaInfoService;
        _logger = logger;
    }

    public string Name => "Restore MediaInfo from .mediainfo files";

    public string Key => "EmbyMediaRestoreMediaInfo";

    public string Description => "Restores technical metadata (MediaStreams, RunTimeTicks, etc.) for media items from .mediainfo backup files.";

    public string Category => "EmbyMedia";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("EmbyMedia Restore Task started.");

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video, MediaType.Audio }, // Include both if needed
            Recursive = true,
            IsMissing = false // Only items that exist
        });

        var totalItems = items.Count;
        var processed = 0;
        var restoredCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if item lacks key metadata (e.g., RunTimeTicks, MediaStreams)
            var hasMediaInfo = item.RunTimeTicks.HasValue && item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item)).Count > 0 && item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item))[0].MediaStreams.Count > 0;

            if (!hasMediaInfo)
            {
                _logger.LogDebug("Attempting restore for item without MediaInfo: {ItemPath}", item.Path);
                var restoreResult = await _mediaInfoService.RestoreMediaInfoAsync(item, cancellationToken);
                if (restoreResult)
                {
                    restoredCount++;
                    _logger.LogInformation("Successfully restored MediaInfo for {ItemPath}", item.Path);
                }
            }

            processed++;
            var percentComplete = (double)processed / totalItems * 100;
            progress.Report(percentComplete);

            if (processed % 100 == 0) // Log progress every 100 items
            {
                _logger.LogInformation("Restore Task Progress: {Processed}/{Total} items processed.", processed, totalItems);
            }
        }

        _logger.LogInformation("EmbyMedia Restore Task completed. Processed {Processed}, Restored {Restored}.", processed, restoredCount);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Example: Run daily at 3 AM
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }
}
