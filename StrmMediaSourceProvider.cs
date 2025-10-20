using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
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

        public async Task<List<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
        {
            if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return new List<MediaSourceInfo>();

            var backupPath = _mediaInfoService.GetBackupPath(item);
            if (File.Exists(backupPath))
            {
                try
                {
                    var mediaSource = _mediaInfoService.DeserializeFromFile<MediaSourceInfo>(backupPath);
                    if (mediaSource != null)
                    {
                        mediaSource.Path = item.Path;
                        mediaSource.Protocol = MediaProtocol.File;
                        mediaSource.IsRemote = false;
                        return new List<MediaSourceInfo> { mediaSource };
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("evermedia: Failed to load .medinfo for {Path}", ex, item.Path);
                }
            }

            return new List<MediaSourceInfo>();
        }

        public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
        {
            return Task.FromResult<ILiveStream>(null);
        }
    }
}
