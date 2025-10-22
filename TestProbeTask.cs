using MediaBrowser.Controller.Library;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class TestProbeTask : IScheduledTask
    {
        private readonly IMediaInfoApi _mediaInfoApi;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;

        public TestProbeTask(
            IMediaInfoApi mediaInfoApi,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogManager logManager)
        {
            _mediaInfoApi = mediaInfoApi;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _logger = logManager.GetLogger(nameof(TestProbeTask));
        }

        public string Name => "Test .strm Probe";
        public string Key => "TestStrmProbe";
        public string Description => "临时任务：验证 PlaybackInfo 能否持久化 .strm 的 MediaInfo";
        public string Category => "Library";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("Starting test probe task...");

            var user = _userManager.Users.Length > 0 ? _userManager.Users[0] : null;
            if (user == null)
            {
                _logger.Error("No user found. Cannot proceed.");
                return;
            }

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "Video" },
                IsVirtualItem = true,
                HasMediaStreams = false
            };
            var strmItems = _libraryManager.GetItemList(query)
                .FindAll(i => i.Path?.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) == true);

            if (strmItems.Count == 0)
            {
                _logger.Info("No .strm items with missing MediaInfo found.");
                return;
            }

            _logger.Info($"Found {strmItems.Count} .strm items to test.");

            for (int i = 0; i < strmItems.Count; i++)
            {
                var item = strmItems[i];
                _logger.Info($"Probing {item.Name} ({item.Path})...");

                try
                {
                    var request = new PlaybackInfoRequest
                    {
                        Id = item.Id.ToString(),
                        UserId = user.Id.ToString(),
                        IsPlayback = false,
                        AutoOpenLiveStream = false
                    };

                    var response = await _mediaInfoApi.GetPlaybackInfo(request, cancellationToken);
                    _logger.Info($"Probe successful for {item.Name}. MediaSources: {response.MediaSources?.Length ?? 0}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Probe failed for {item.Name}: {ex.Message}");
                }

                progress.Report((double)(i + 1) / strmItems.Count * 100);
                await Task.Delay(100, cancellationToken);
            }

            _logger.Info("Test probe task completed.");
        }
    }
}
