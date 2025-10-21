using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
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
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        // 并发控制：BaseItem.Id 是 Guid
        private static readonly ConcurrentDictionary<Guid, Task> _ongoingTasks = new();

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

        public async Task BackupMediaInfoAsync(BaseItem? item)
        {
            if (item?.Id == Guid.Empty)
                return;

            var task = _ongoingTasks.GetOrAdd(item.Id, _ => ProcessItemInternalAsync(item));
            await task;
        }

        private async Task ProcessItemInternalAsync(BaseItem item)
        {
            try
            {
                var mediaSource = await ProbeAndExtractMediaInfoAsync(item);
                if (mediaSource == null)
                {
                    _logger.Debug("Probing did not yield a media source for '{Name}'. Skipping.", item.Name);
                    return;
                }

                int videoCount = 0, audioCount = 0;
                foreach (var stream in mediaSource.MediaStreams)
                {
                    if (stream.Type == MediaStreamType.Video) videoCount++;
                    else if (stream.Type == MediaStreamType.Audio) audioCount++;
                }

                _logger.Info("Successfully probed media info for '{Name}': " +
                             "Container={Container}, Duration={Duration}s, VideoStreams={V}, AudioStreams={A}",
                    item.Name,
                    mediaSource.Container,
                    (mediaSource.RunTimeTicks / TimeSpan.TicksPerSecond),
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

        private async Task<MediaSourceInfo?> ProbeAndExtractMediaInfoAsync(BaseItem item)
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

            // 跳过远程 URL
            if (realPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                realPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("Skipping probe for remote URL: {Url}", realPath);
                return null;
            }

            try
            {
                // ✅ 正确调用方式：直接传路径 + ProfileType
                var mediaSource = await _mediaEncoder.GetMediaInfo(
                    realPath,
                    ProfileType.Video,
                    CancellationToken.None);
                return mediaSource;
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
