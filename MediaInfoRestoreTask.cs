using MediaBrowser.Controller.Entities; ///for InternalItemsQuery 
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class MediaInfoRestoreTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly MediaInfoService _mediaInfoService;

        public MediaInfoRestoreTask(ILibraryManager libraryManager, ILogger logger, MediaInfoService mediaInfoService)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _mediaInfoService = mediaInfoService;
        }

        public string Name => "evermedia: Restore MediaInfo from .medinfo";
        public string Key => "EvermediaRestoreMediaInfo";
        public string Description => "Restore MediaInfo for STRM files from .medinfo backups.";
        public string Category => "evermedia";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            };
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            // 使用 InternalItemsQuery（Emby 4.9）
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                MediaTypes = new[] { MediaType.Video },
                Recursive = true,
                IsVirtualItem = true
            });

            if (items.Length == 0) return;

            int total = items.Length, done = 0, restored = 0;
            foreach (var item in items)
            {
                if (item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) == true &&
                    !item.RunTimeTicks.HasValue)
                {
                    if (await _mediaInfoService.RestoreMediaInfoAsync(item, cancellationToken))
                        restored++;
                }
                progress.Report(++done * 100.0 / total);
            }
            _logger.Info("evermedia: Restore task completed. Restored {Count} items.", restored);
        }
    }
}
