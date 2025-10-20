using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;           // 👈 必须：MediaSourceInfo
using MediaBrowser.Model.LiveTv;       // 👈 必须：ILiveStream
using MediaBrowser.Model.Logging;
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

        // ✅ 正确返回 Task<List<MediaSourceInfo>>
        public async Task<List<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
        {
            if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return new List<MediaSourceInfo>();

            var backupPath = _mediaInfoService.GetBackupPath(item);
            if (File.Exists(backupPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(backupPath, cancellationToken);
                    var mediaSource = Newtonsoft.Json.JsonConvert.DeserializeObject<MediaSourceInfo>(json);
                    if (mediaSource != null)
                    {
                        mediaSource.Path = item.Path;
                        mediaSource.Protocol = MediaBrowser.Model.Entities.MediaProtocol.File;
                        mediaSource.IsRemote = false;
                        return new List<MediaSourceInfo> { mediaSource };
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("evermedia: Failed to load .medinfo", ex);
                }
            }

            return new List<MediaSourceInfo>();
        }

        // ✅ 必须实现 OpenMediaSource，返回 Task<ILiveStream>
        public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
        {
            // STRM 不涉及直播流，返回 null
            return Task.FromResult<ILiveStream>(null);
        }
    }
}
