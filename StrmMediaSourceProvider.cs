using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization; // 👈 IJsonSerializer
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
        private readonly IJsonSerializer _jsonSerializer; // 👈 直接注入

        public StrmMediaSourceProvider(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        public string Name => "evermedia STRM Provider";

        public async Task<List<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
        {
            if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
                return new List<MediaSourceInfo>();

            // 构造 .medinfo 路径（与 MediaInfoService 一致）
            var backupPath = Path.ChangeExtension(item.Path, ".medinfo");

            if (File.Exists(backupPath))
            {
                try
                {
                    // ✅ 直接使用 IJsonSerializer 反序列化
                    var mediaSource = _jsonSerializer.DeserializeFromFile<MediaSourceInfo>(backupPath);
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
