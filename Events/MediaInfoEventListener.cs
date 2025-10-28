// Events/MediaInfoEventListener.cs
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Concurrent; // For ConcurrentDictionary
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks; // For Task.Delay and Task
using EverMedia.Services; // 引入 MediaInfoService
using EverMedia.Configuration; // 引入配置类
using MediaBrowser.Model.IO; // 引入 IFileSystem
using System.Linq; // For OfType
using System.IO; // For Path
using MediaBrowser.Model.Entities; // For MediaStreamType

namespace EverMedia.Events; // 使用命名空间组织代码

/// <summary>
/// 事件监听器：负责监听 Emby 的 ItemAdded 和 ItemUpdated 事件，
/// 并触发相应的 MediaInfoService 逻辑。
/// </summary>
public class EverMediaEventListener : IAsyncDisposable // Implement IAsyncDisposable for proper cleanup of timers
{
    // --- 依赖注入的私有字段 ---
    private readonly ILogger _logger;
    private readonly EverMediaService _everMediaService;
    private readonly IFileSystem _fileSystem; // 需要 IFileSystem 来检查 .medinfo 文件是否存在

    // --- 事件防抖相关 ---
    // 使用 ConcurrentDictionary 存储每个 Item 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _debounceTokens = new();

    // --- 构造函数：接收依赖项 ---
    public EverMediaEventListener(
        ILogger logger,           // 用于记录事件处理日志
        EverMediaService evermediaService, // 用于执行备份和恢复逻辑
        IFileSystem fileSystem // 用于检查 .medinfo 文件
    )
    {
        _logger = logger;
        _everMediaService = evermediaService;
        _fileSystem = fileSystem;
    }

    // --- 事件处理方法：处理 ItemAdded 事件 ---
    public async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"[EverMediaEventListener] ItemAdded event triggered for .strm file: {item.Path}");

            string medInfoPath = _everMediaService.GetMedInfoPath(item);
            if (_fileSystem.FileExists(medInfoPath))
            {
                _logger.Info($"[EverMediaEventListener] .medinfo file found for added item: {item.Path}. Attempting quick restore.");
                await _everMediaService.RestoreAsync(item);
            }
            else
            {
                _logger.Debug($"[EverMediaEventListener] No .medinfo file found for added item: {item.Path}. Delegating to bootstrap task.");
            }
        }
    }

    // --- 事件处理方法：处理 ItemUpdated 事件 ---
    public async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug($"[EverMediaEventListener] ItemUpdated event received for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count before debounce: {(item.MediaStreams?.Count ?? 0)}");
    
            var itemId = item.Id;
            if (_debounceTokens.TryGetValue(itemId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }
            var newCts = new CancellationTokenSource();
            _debounceTokens[itemId] = newCts;
    
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), newCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce cancelled for item: {item.Path}");
                return;
            }
            finally
            {
                _debounceTokens.TryRemove(itemId, out _);
                newCts.Dispose();
            }
    
            _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce completed for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count after debounce: {(item.MediaStreams?.Count ?? 0)}");
    
            var mediaStreams = item.GetMediaStreams();
            var hasVideoOrAudio = mediaStreams?.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio) == true;
            var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;
    
            string medInfoPath = _everMediaService.GetMedInfoPath(item);
            bool medInfoExists = _fileSystem.FileExists(medInfoPath);
    
            _logger.Debug($"[EverMediaEventListener] Checking criteria for {item.Path}. HasVideoOrAudio: {hasVideoOrAudio}, HasSubtitles: {hasSubtitles}, MedInfoExists: {medInfoExists}");
    
            // ✅ 新增逻辑：检测到仅有字幕流（无音视频）且存在 .medinfo → 删除 .medinfo 并触发 probe
            if (hasSubtitles && !hasVideoOrAudio && medInfoExists)
            {
                _logger.Info($"[EverMediaEventListener] Detected subtitle-only update for {item.Path}. Deleting .medinfo and triggering FFProbe to refresh full MediaInfo.");
                
                try
                {
                    _fileSystem.DeleteFile(medInfoPath);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[EverMediaEventListener] Failed to delete .medinfo file at {medInfoPath}: {ex.Message}");
                }
    
                // ✅ 触发 probe：复用 BootstrapTask 的逻辑
                var directoryService = new DirectoryService(_logger, _fileSystem);
                var refreshOptions = new MetadataRefreshOptions(directoryService)
                {
                    EnableRemoteContentProbe = true,
                    MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllMetadata = false,
                    ImageRefreshMode = MetadataRefreshMode.ValidationOnly,
                    ReplaceAllImages = false,
                    EnableThumbnailImageExtraction = false,
                    EnableSubtitleDownloading = false
                };
    
                try
                {
                    await item.RefreshMetadata(refreshOptions, CancellationToken.None);
                    _logger.Info($"[EverMediaEventListener] FFProbe triggered successfully for {item.Path}.");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[EverMediaEventListener] Failed to trigger FFProbe for {item.Path}: {ex.Message}");
                }
    
                return; // 不再执行恢复或备份
            }
    
            // 原有逻辑：自愈
            if (!hasVideoOrAudio && medInfoExists)
            {
                _logger.Info($"[EverMediaEventListener] Self-heal detected for item: {item.Path}. No MediaInfo, .medinfo exists. Attempting restore.");
                await _everMediaService.RestoreAsync(item);
            }
            // 原有逻辑：机会性备份
            else if (hasVideoOrAudio && !medInfoExists)
            {
                _logger.Info($"[EverMediaEventListener] Opportunity backup detected for item: {item.Path}. MediaInfo exists, .medinfo missing. Attempting backup.");
                await _everMediaService.BackupAsync(item);
            }
            else
            {
                _logger.Debug($"[EverMediaEventListener] ItemUpdated event for {item.Path} did not meet self-heal or backup criteria. HasVideoOrAudio: {hasVideoOrAudio}, MedInfoExists: {medInfoExists}");
            }
        }
    }

//
//
//    // --- 事件处理方法：处理 ItemUpdated 事件 ---
//    public async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
//    {
//        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
//        {
//            _logger.Debug($"[EverMediaEventListener] ItemUpdated event received for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count before debounce: {(item.MediaStreams?.Count ?? 0)}");
//
//            var itemId = item.Id;
//            if (_debounceTokens.TryGetValue(itemId, out var existingCts))
//            {
//                existingCts.Cancel();
//                existingCts.Dispose();
//            }
//
//            var newCts = new CancellationTokenSource();
//            _debounceTokens[itemId] = newCts;
//
//            try
//            {
//                await Task.Delay(TimeSpan.FromSeconds(1), newCts.Token);
//            }
//            catch (OperationCanceledException)
//            {
//                _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce cancelled for item: {item.Path}");
//                return;
//            }
//            finally
//            {
//                _debounceTokens.TryRemove(itemId, out _);
//                newCts.Dispose();
//            }
//
//            // _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce completed for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count after debounce: {(item.MediaStreams?.Count ?? 0)}");
//
//            var mediaStreams = item.GetMediaStreams();
//            var hasVideoOrAudio = mediaStreams?.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio) == true;
//            var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;
//
//            string medInfoPath = _everMediaService.GetMedInfoPath(item);
//            bool medInfoExists = _fileSystem.FileExists(medInfoPath);
//            
//            _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce completed for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count after debounce: {(item.MediaStreams?.Count ?? 0)}");
//
//            _logger.Debug($"[EverMediaEventListener] Checking criteria for {item.Path}. HasVideoOrAudio: {hasVideoOrAudio}, HasSubtitles: {hasSubtitles}, MedInfoExists: {medInfoExists}");
//
//            // ✅ 新增逻辑：检测到仅有字幕流（无音视频）且存在 .medinfo → 删除 .medinfo
//            if (hasSubtitles && !hasVideoOrAudio && medInfoExists)
//            {
//                _logger.Info($"[EverMediaEventListener] Detected subtitle-only update for {item.Path}. Deleting .medinfo to allow future backup with new streams.");
//                try
//                {
//                    _fileSystem.DeleteFile(medInfoPath);
//                }
//                catch (Exception ex)
//                {
//                    _logger.Error($"[EverMediaEventListener] Failed to delete .medinfo file at {medInfoPath}: {ex.Message}");
//                }
//                return; // 不再执行恢复或备份
//            }
//
//            // 原有逻辑：自愈
//            if (!hasVideoOrAudio && medInfoExists)
//            {
//                _logger.Info($"[EverMediaEventListener] Self-heal detected for item: {item.Path}. No MediaInfo, .medinfo exists. Attempting restore.");
//                await _everMediaService.RestoreAsync(item);
//            }
//            // 原有逻辑：机会性备份
//            else if (hasVideoOrAudio && !medInfoExists)
//            {
//                _logger.Info($"[EverMediaEventListener] Opportunity backup detected for item: {item.Path}. MediaInfo exists, .medinfo missing. Attempting backup.");
//                await _everMediaService.BackupAsync(item);
//            }
//            else
//            {
//                _logger.Debug($"[EverMediaEventListener] ItemUpdated event for {item.Path} did not meet self-heal or backup criteria. HasVideoOrAudio: {hasVideoOrAudio}, MedInfoExists: {medInfoExists}");
//            }
//        }
//    }

    // --- Cleanup ---
    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _debounceTokens)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _debounceTokens.Clear();
    }
}
