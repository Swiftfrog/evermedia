//ServerEntryPoint.cs

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace evermedia
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly MediaInfoService _mediaInfoService;
        private readonly ILogger _logger;

        public ServerEntryPoint(
            ILibraryManager libraryManager,
            MediaInfoService mediaInfoService,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaInfoService = mediaInfoService;
            _logger = logManager.GetLogger(nameof(ServerEntryPoint));
        }

        public void Run()
        {
            _libraryManager.ItemAdded += OnLibraryItemChanged;
            _libraryManager.ItemUpdated += OnLibraryItemChanged;
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= OnLibraryItemChanged;
            _libraryManager.ItemUpdated -= OnLibraryItemChanged;
        }

        private async void OnLibraryItemChanged(object? sender, ItemChangeEventArgs e)
        {
            try
            {
                var item = e.Item;

                if (item == null || string.IsNullOrEmpty(item.Path))
                    return;

                if (!item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                    return;

                // ✅ 修复：ItemUpdateType 不包含 PlaybackStart/Stop
                // 改为检查是否仅为 UserData 变更（播放进度等）
                if (e.UpdateReason == ItemUpdateType.None || 
                    e.UpdateReason == ItemUpdateType.ImageUpdate)
                {
                    // 可能是播放状态变更，跳过
                    return;
                }

                await _mediaInfoService.BackupMediaInfoAsync(item);
            }
            catch (Exception ex)
            {
                // ✅ 修复：ILogger 不支持 exception 参数
                _logger.Error("Unexpected error in OnLibraryItemChanged: {Message}", ex.Message);
            }
        }
    }
}
