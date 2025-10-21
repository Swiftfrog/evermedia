//MediaInfoService.cs
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.IO;
using MediaBrowser.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;

namespace evermedia
{
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        // 🔒 并发控制：防止同一 item 被重复处理
        private static readonly ConcurrentDictionary<long, Task> _ongoingTasks = new();

        public MediaInfoService(
            ILibraryManager libraryManager,
            IMediaEncoder mediaEncoder,
            IFileSystem fileSystem,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _fileSystem = fileSystem;
            _logger = logManager.GetLogger(nameof(MediaInfoService));
        }

        /// <summary>
        /// 入口方法：对外提供线程安全的探测与备份接口
        /// </summary>
        public async Task BackupMediaInfoAsync(BaseItem? item)
        {
            if (item?.Id is not {} id)
                return;

            // 防重入：同一个 item.Id 只允许一个任务执行
            var task = _ongoingTasks.GetOrAdd(id, _ => ProcessItemInternalAsync(item));
            await task;
        }

        /// <summary>
        /// 实际处理逻辑（仅由一个任务执行）
        /// </summary>
        private async Task ProcessItemInternalAsync(BaseItem item)
        {
            try
            {
                var probeResult = await ProbeAndExtractMediaInfoAsync(item);
                if (probeResult?.MediaSources == null || probeResult.MediaSources.Count == 0)
                {
                    _logger.Debug("Probing did not yield media sources for '{Name}'. Skipping.", item.Name);
                    return;
                }

                // 取第一个 MediaSource（.strm 通常只有一个）
                var mediaSource = probeResult.MediaSources[0];

                // ✅ 验证阶段：仅打印探测结果，暂不写入 .medinfo 或数据库
                _logger.Info("Successfully probed media info for '{Name}': " +
                             "Container={Container}, Duration={Duration}s, VideoStreams={V}, AudioStreams={A}",
                    item.Name,
                    mediaSource.Container,
                    (mediaSource.RunTimeTicks / TimeSpan.TicksPerSecond),
                    mediaSource.MediaStreams.Count(s => s.Type == MediaStreamType.Video),
                    mediaSource.MediaStreams.Count(s => s.Type == MediaStreamType.Audio));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while probing '{Name}'", item.Name);
            }
            finally
            {
                // 清理并发控制字典
                _ongoingTasks.TryRemove(item.Id, out _);
            }
        }

        /// <summary>
        /// 探测 .strm 指向的真实媒体路径
        /// </summary>
        private async Task<MediaProbeResult?> ProbeAndExtractMediaInfoAsync(BaseItem item)
        {
            if (string.IsNullOrEmpty(item.Path))
                return null;

            string realPath;
            try
            {
                realPath = await _fileSystem.ReadAllTextAsync(item.Path);
                realPath = realPath.Trim();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to read .strm file: {Path}", item.Path);
                return null;
            }

            if (string.IsNullOrEmpty(realPath))
            {
                _logger.Warn("'.strm' file is empty: {Path}", item.Path);
                return null;
            }

            // 🚫 不探测远程 URL（阶段二验证聚焦本地文件）
            if (realPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                realPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("Skipping probe for remote URL: {Url}", realPath);
                return null;
            }

            // ✅ 仅探测本地/UNC 路径
            try
            {
                var request = new MediaInfoRequest
                {
                    Path = realPath,
                    MediaType = Dlna.ProfileType.Video
                };

                var result = await _mediaEncoder.GetMediaInfo(request, CancellationToken.None);
                return result;
            }
            catch (FileNotFoundException)
            {
                _logger.Warn("Media file not found: {Path} (referenced by {StrmPath})", realPath, item.Path);
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                _logger.Warn("Access denied to media file: {Path}", realPath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to probe media at '{Path}'", realPath);
                return null;
            }
        }
    }
}
