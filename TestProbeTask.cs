using MediaBrowser.Controller.Entities;      // ← InternalItemsQuery
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class TestProbeTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public TestProbeTask(
            ILibraryManager libraryManager,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _logger = logManager.GetLogger(nameof(TestProbeTask));
        }

        public string Name => "Test .strm Refresh with Probe";
        public string Key => "TestStrmRefreshWithProbe";
        public string Description => "临时任务：验证 QueueRefresh + EnableRemoteContentProbe 能否持久化 .strm 的 MediaInfo";
        public string Category => "Library";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("Starting test refresh task with EnableRemoteContentProbe...");

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Video" },
                IsVirtualItem = true,
                HasMediaStreams = false
            };

            // GetItemList 返回 BaseItem[]
            var allItems = _libraryManager.GetItemList(query);
            var strmItems = new List<BaseItem>();

            foreach (var item in allItems)
            {
                if (item.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) == true)
                {
                    strmItems.Add(item);
                }
            }

            if (strmItems.Count == 0)
            {
                _logger.Info("No .strm items with missing MediaInfo found.");
                return;
            }

            _logger.Info($"Found {strmItems.Count} .strm items to test.");

            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_logger, _fileSystem))
            {
                EnableRemoteContentProbe = true,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllMetadata = false,
                ImageRefreshMode = MetadataRefreshMode.ValidationOnly,
                ReplaceAllImages = false
            };

            for (int i = 0; i < strmItems.Count; i++)
            {
                var item = strmItems[i];
                _logger.Info($"Queueing refresh for {item.Name} ({item.Path})...");

                try
                {
                    _providerManager.QueueRefresh(
                        item.Id,
                        refreshOptions,
                        RefreshPriority.High);

                    _logger.Info($"Refresh queued for {item.Name}.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to queue refresh for {item.Name}: {ex.Message}");
                }

                progress.Report((double)(i + 1) / strmItems.Count * 100);
                await Task.Delay(100, cancellationToken);
            }

            _logger.Info("Test refresh task completed. Check logs for FFProbeProvider activity.");
        }
    }
}
