using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace evermedia
{
    public class MediaInfoService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<MediaInfoService> _logger;

        // 1. 添加用于并发控制的静态字典
        private static readonly ConcurrentDictionary<long, Task> _ongoingTasks = new ConcurrentDictionary<long, Task>();

        public MediaInfoService(ILibraryManager libraryManager, IMediaEncoder mediaEncoder, IFileSystem fileSystem, ILogger<MediaInfoService> logger)
        {
            _libraryManager = libraryManager;
            _mediaEncoder = mediaEncoder;
            _fileSystem = fileSystem;
            _logger = logger;
        }

        // 2. 新的公共方法，负责并发控制
        public async Task BackupMediaInfoAsync(BaseItem item)
        {
            // 使用 GetOrAdd 原子性地为每个 item.Id 添加一个处理任务
            var task = _ongoingTasks.GetOrAdd(item.Id, id => ProcessItemInternalAsync(item));

            try
            {
                // 等待任务完成（无论是新建的还是已存在的）
                await task;
            }
            finally
            {
                // 任务完成后，从字典中移除，以便下次可以重新探测
                _ongoingTasks.TryRemove(item.Id, out _);
            }
        }

        // 3. 将原有逻辑移入这个新的私有方法
        private async Task ProcessItemInternalAsync(BaseItem item)
        {
            var probeResult = await ProbeAndExtractMediaInfoAsync(item);

            if (probeResult?.MediaSources == null |

| probeResult.MediaSources.Count == 0)
            {
                _logger.LogWarning("evermedia: Probe did not yield valid MediaInfo for '{Path}'.", item.Path);
                return;
            }

            var mediaSource = probeResult.MediaSources;

            // 数据净化与序列化 (此处简化，实际应按TDD进行严格净化)
            var backupData = new { MediaSourceInfo = mediaSource, Chapters = probeResult.Chapters };
            var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true });

            // 写入.medinfo 文件
            var medinfoPath = item.Path + ".medinfo";
            await _fileSystem.WriteAllTextAsync(medinfoPath, json);
            _logger.LogInformation("evermedia: MediaInfo for '{Name}' backed up to '{Path}'.", item.Name, medinfoPath);

            // 更新 Emby 数据库
            await UpdateDatabaseAsync(item, mediaSource, probeResult.Chapters);
        }

        private async Task<MediaProbeResult> ProbeAndExtractMediaInfoAsync(BaseItem item)
        {
            var realPath = (await _fileSystem.ReadAllTextAsync(item.Path)).Trim();

            if (string.IsNullOrEmpty(realPath))
            {
                _logger.LogWarning("evermedia:.strm file '{Path}' is empty.", item.Path);
                return null;
            }

            bool isRemote = realPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            realPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (isRemote)
            {
                _logger.LogInformation("evermedia: '{Name}' points to a remote URL. Probing is not supported.", item.Name);
                return null;
            }

            var request = new MediaInfoRequest { Path = realPath };
            try
            {
                return await _mediaEncoder.GetMediaInfo(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "evermedia: Failed to probe media info for '{Name}' at path '{Path}'.", item.Name, realPath);
                return null;
            }
        }

        private async Task UpdateDatabaseAsync(BaseItem item, MediaSourceInfo mediaSource, System.Collections.Generic.List<ChapterInfo> chapters)
        {
            item.RunTimeTicks = mediaSource.RunTimeTicks;
            item.Container = mediaSource.Container;
            // 根据需要添加更多顶级属性的同步

            _libraryManager.SaveMediaStreams(item.Id, mediaSource.MediaStreams);
            
            // 对于 Emby 4.7+, 需要提供 MetadataRefreshOptions
            var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem));
            await _libraryManager.UpdateItemAsync(item, item.Parent, ItemUpdateType.MetadataEdit, refreshOptions, CancellationToken.None);

            _logger.LogInformation("evermedia: Database updated for '{Name}'.", item.Name);
        }
    }
}
