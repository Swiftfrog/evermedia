using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging; // 使用 Emby 的 ILogger
using MediaBrowser.Model.Tasks; // IScheduledTask, TaskTriggerInfo
using MediaBrowser.Controller.Entities; //BaseItem
using MediaBrowser.Model.Entities; // MediaType InternalItemsQuery
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

public class MediaInfoRestoreTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger; // 使用 Emby 的非泛型 ILogger

    public MediaInfoRestoreTask(ILibraryManager libraryManager, ILogger logger) // 依赖注入 Emby 的 ILogger
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "Restore MediaInfo from .mediainfo files";

    public string Key => "EmbyMediaRestoreMediaInfo";

    public string Description => "Restores technical metadata (MediaStreams, RunTimeTicks, etc.) for media items from .mediainfo backup files.";

    public string Category => "EmbyMedia";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Example: Run daily at 3 AM
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfo.TriggerDaily,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }

    // Emby 的 IScheduledTask.Execute 方法签名
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("EmbyMedia Restore Task started."); // 使用 Emby ILogger 的 Info 方法

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video, MediaType.Audio }, // Include both if needed
            Recursive = true,
            IsMissing = false // Only items that exist
        });

        var totalItems = items.Count; // Note: GetItemList returns List<T>, so Count is correct
        var processed = 0;
        var restoredCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if item lacks key metadata (e.g., RunTimeTicks, MediaStreams)
            var hasMediaInfo = item.RunTimeTicks.HasValue && item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item)).Count > 0 && item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item))[0].MediaStreams.Count > 0;

            if (!hasMediaInfo)
            {
                _logger.Debug("Attempting restore for item without MediaInfo: {0}", item.Path); // 使用 Emby ILogger 的 Debug 方法
                // 注意：这里需要注入 IMediaInfoService 或直接在此类中实现恢复逻辑
                // var restoreResult = await _mediaInfoService.RestoreMediaInfoAsync(item, cancellationToken);
                // For now, assume a placeholder or direct implementation
                var restoreResult = await RestoreMediaInfoAsync(item, cancellationToken); // Placeholder call
                if (restoreResult)
                {
                    restoredCount++;
                    _logger.Info("Successfully restored MediaInfo for {0}", item.Path); // 使用 Emby ILogger 的 Info 方法
                }
            }

            processed++;
            var percentComplete = (double)processed / totalItems * 100;
            progress.Report(percentComplete);

            if (processed % 100 == 0) // Log progress every 100 items
            {
                _logger.Info("Restore Task Progress: {0}/{1} items processed.", processed, totalItems); // 使用 Emby ILogger 的 Info 方法
            }
        }

        _logger.Info("EmbyMedia Restore Task completed. Processed {0}, Restored {1}.", processed, restoredCount); // 使用 Emby ILogger 的 Info 方法
    }

    // Placeholder method - implement the actual restore logic here or via injected service
    private async Task<bool> RestoreMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        // Implement restore logic here, similar to the service but using Emby's ILogger
        // This is a simplified placeholder
        _logger.Info("Restoring MediaInfo for {0} - Placeholder", item.Path);
        // ... actual restore code ...
        return true; // Placeholder return
    }
}
