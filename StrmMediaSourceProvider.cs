using MediaBrowser.Controller.Library; ///IMediaSourceProvider
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Dto; ///MediaSourceInfo
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace evermedia
{
    public class StrmMediaSourceProvider : IMediaSourceProvider
    {
        private readonly ILogger _logger;
        private readonly MediaInfoService _mediaInfoService;

        public StrmMediaSourceProvider(ILogger logger, MediaInfoService mediaInfoService)
        {
            _logger = logger;
            _mediaInfoService = mediaInfoService;
        }

        public string Name => "evermedia STRM Provider";

        public async Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
        {
            if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return [];

            // 优先从 .medinfo 恢复
            var backupPath = _mediaInfoService.GetBackupPath(item);
            if (File.Exists(backupPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(backupPath, cancellationToken);
                    var mediaSource = Newtonsoft.Json.JsonConvert.DeserializeObject<MediaSourceInfo>(json);
                    if (mediaSource != null)
                    {
                        // 修复路径（避免泄露真实路径）
                        mediaSource.Path = item.Path;
                        mediaSource.Protocol = MediaProtocol.File;
                        mediaSource.IsRemote = false;
                        return [mediaSource];
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("evermedia: Failed to load .medinfo for {Path}", ex, item.Path);
                }
            }

            // 兜底：尝试实时 probe（可选，此处省略以提升性能）
            return [];
        }
    }
}
