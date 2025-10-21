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

                // 1. 过滤空项或非 .strm 文件
                if (item == null || string.IsNullOrEmpty(item.Path))
                    return;

                if (!item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                    return;

                // 2. 忽略纯播放状态变更（避免刷新时重复触发）
                if (e.UpdateReason == ItemUpdateType.PlaybackStart ||
                    e.UpdateReason == ItemUpdateType.PlaybackStop)
                {
                    return;
                }

                // 3. 委托给服务层处理（自动包含并发控制）
                await _mediaInfoService.BackupMediaInfoAsync(item);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error in OnLibraryItemChanged for item: {Path}", e.Item?.Path ?? "null");
            }
        }
    }
}
