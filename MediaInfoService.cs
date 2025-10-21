using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace evermedia
{
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaProbeManager _mediaProbeManager; // ← 关键：使用 IMediaProbeManager
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        private static readonly ConcurrentDictionary<Guid, Task> _ongoingTasks = new();

        public MediaInfoService(
            ILibraryManager libraryManager,
            IMediaProbeManager mediaProbeManager, // ← 注入 IMediaProbeManager
            IFileSystem fileSystem,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _mediaProbeManager = mediaProbeManager;
            _fileSystem = fileSystem;
            _logger = logManager.GetLogger(nameof(MediaInfoService));
        }

        public async Task BackupMediaInfoAsync(BaseItem? item)
        {
            if (item == null || item.Id == Guid.Empty)
                return;

            var task = _ongoingTasks.GetOrAdd(item.Id, _ => ProcessItemInternalAsync(item));
            await task;
        }

        private async Task ProcessItemInternalAsync(BaseItem item)
        {
            try
            {
                var mediaInfo = await ProbeAndExtractMediaInfoAsync(item);
                if (mediaInfo == null)
                {
                    _logger.Debug("Probing did not yield media info for '{Name}'. Skipping.", item.Name);
                    return;
                }

                int videoCount = 0, audioCount = 0;
                foreach (var stream in mediaInfo.MediaStreams)
                {
                    if (stream.Type == MediaStreamType.Video) videoCount++;
                    else if (stream.Type == MediaStreamType.Audio) audioCount++;
                }

                _logger.Info("Successfully probed media info for '{Name}': " +
                             "Container={Container}, Duration={Duration}s, VideoStreams={V}, AudioStreams={A}",
                    item.Name,
                    mediaInfo.Container,
                    (mediaInfo.RunTimeTicks / TimeSpan.TicksPerSecond),
                    videoCount,
                    audioCount);
            }
            catch (Exception ex)
            {
                _logger.Error("Unexpected error while probing '{Name}': {Message}", item.Name, ex.Message);
            }
            finally
            {
                _ongoingTasks.TryRemove(item.Id, out _);
            }
        }

        private async Task<MediaInfo?> ProbeAndExtractMediaInfoAsync(BaseItem item)
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
                _logger.Warn("Failed to read .strm file {Path}: {Message}", item.Path, ex.Message);
                return null;
            }

            if (string.IsNullOrEmpty(realPath))
            {
                _logger.Warn("'.strm' file is empty: {Path}", item.Path);
                return null;
            }

            if (realPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                realPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("Skipping probe for remote URL: {Url}", realPath);
                return null;
            }

            try
            {
                // ✅ 正确构造 MediaInfoRequest
                var mediaSource = new MediaSourceInfo
                {
                    Path = realPath,
                    Protocol = MediaProtocol.File
                };

                var request = new MediaInfoRequest
                {
                    MediaSource = mediaSource,
                    MediaType = DlnaProfileType.Video,
                    ExtractChapters = false
                };

                var mediaInfo = await _mediaProbeManager.GetMediaInfo(request, CancellationToken.None);
                return mediaInfo;
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
                _logger.Error("Failed to probe media at '{Path}': {Message}", realPath, ex.Message);
                return null;
            }
        }
    }
}
