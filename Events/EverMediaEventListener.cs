// Events/EverMediaEventListener.cs
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using EverMedia.Services;

namespace EverMedia.Events;

/// <summary>
/// 事件监听器：负责监听 Emby 的 ItemAdded 和 ItemUpdated 事件，
/// 并触发相应的 MediaInfoService 逻辑。
/// </summary>
public class EverMediaEventListener : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly EverMediaService _everMediaService;
    private readonly IFileSystem _fileSystem;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _debounceTokens = new();
    private readonly ConcurrentDictionary<Guid, (int Count, DateTime LastAttempt)> _probeFailureTracker = new();
    private readonly TimeSpan _shortTermRetryDelay = TimeSpan.FromSeconds(10);

    public EverMediaEventListener(
        ILogger logger,
        EverMediaService everMediaService,
        IFileSystem fileSystem)
    {
        _logger = logger;
        _everMediaService = everMediaService;
        _fileSystem = fileSystem;
    }

    // --- ItemAdded: 新增 .strm 文件处理 ---
    public async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        try
        {
	        var config = Plugin.Instance.Configuration;
	        if (config == null || !config.EnablePlugin)
	        {
	            _logger.Debug($"[EverMedia] EventListener: Plugin is disabled. Ignoring ItemAdded event for {e.Item.Name}.");
	            return;
	        }
            
            if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"[EverMedia] EventListener: ItemAdded event triggered for .strm file: '{item.Name ?? item.Path}' (ID: {item.Id})");

                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                if (medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: .medinfo file found for added item: '{item.Name ?? item.Path}' (ID: {item.Id}). Attempting quick restore.");
                    await _everMediaService.RestoreAsync(item);
                }
                else
                {
                    _logger.Info($"[EverMedia] EventListener: No .medinfo file found for added item: '{item.Name ?? item.Path}' (ID: {item.Id}). Triggering FFProbe to fetch MediaInfo.");
                    await TriggerFullProbeAsync(item);
                }
            }
        }
        catch (Exception ex)
        {
            string itemName = (e?.Item as BaseItem)?.Name ?? "Unknown";
            _logger.Error($"[EverMedia] EventListener: Unhandled exception in OnItemAdded for '{itemName}': {ex.Message}");
            _logger.Debug($"Stack trace for OnItemAdded exception: {ex}");
        }
    }

    // --- ItemUpdated: 更新事件处理（含字幕刷新）---
    public async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        try
        {
	        var config = Plugin.Instance.Configuration;
	        if (config == null || !config.EnablePlugin)
	        {
	            _logger.Debug($"[EverMedia] EventListener: Plugin is disabled. Ignoring ItemUpdated event for {e.Item.Name}.");
	            return;
	        }

            if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"[EverMedia] EventListener: ItemUpdated debounce received for .strm file: {item.Name} (ID: {item.Id}).");

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
                    _logger.Debug($"[EverMedia] EventListener: ItemUpdated debounce cancelled for item: {item.Name}");
                    return;
                }
                finally
                {
                    _debounceTokens.TryRemove(itemId, out _);
                    newCts.Dispose();
                }

                _logger.Debug($"[EverMedia] EventListener: ItemUpdated debounce completed for .strm file: {item.Name} (ID: {item.Id}).");

                var mediaStreams = item.GetMediaStreams();
                var hasVideoOrAudio = mediaStreams?.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio) == true;
                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                _logger.Debug($"[EverMedia] EventListener: Checking criteria for {item.Name ?? item.Path} (ID: {item.Id}). HasV/A: {hasVideoOrAudio}, HasMedinfo: {medInfoExists}");

                if (!hasVideoOrAudio && medInfoExists)
                {
                    // 场景 1: 歧义状态
                    _logger.Info($"[EverMedia] EventListener: Ambiguous state (V/A loss, medinfo exists) for {item.Name ?? item.Path} (ID: {item.Id}). Comparing sub counts...");
                    
                    int savedExternalCount = _everMediaService.GetSavedExternalSubCount(item);
                    int currentExternalCount = mediaStreams?.Count(s => s.Type == MediaStreamType.Subtitle && s.IsExternal) ?? 0;
                
                    _logger.Debug($"[EverMedia] EventListener: Counts for '{item.Name ?? item.Path}'. SavedInMedinfo: {savedExternalCount}, CurrentFromGetMediaStreams: {currentExternalCount}");
                
                    if (currentExternalCount != savedExternalCount)
                    {
                        // 场景 1a: 字幕变更，触发FFProbe
                        _logger.Info($"[EverMedia] EventListener: Subtitle count mismatch. Assuming subtitle change for {item.Name ?? item.Path}. Deleting stale .medinfo and triggering FFProbe.");
                        try
                        {
                            _fileSystem.DeleteFile(medInfoPath);    // 删除.medinfo，防止从旧的.medinfo恢复
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.Error($"[EverMedia] EventListener: Failed to delete .medinfo file at {medInfoPath}: {deleteEx.Message}");
                        }
                        await TriggerFullProbeAsync(item);    // 重新触发FFProbe, 如果失败，会执行到场景3.
                    }
                    else
                    {
                        // 场景 1b: 从.medinfo恢复
                        _logger.Info($"[EverMedia] EventListener: Subtitle count matches for '{item.Name ?? item.Path}'. Performing fast self-heal restore.");
                        await _everMediaService.RestoreAsync(item);
                    }
                    
                    // ** 清理 **：如果项目被成功自愈 (场景 1b)，它就是健康的
                    if (currentExternalCount == savedExternalCount)
                    {
                         _probeFailureTracker.TryRemove(item.Id, out _);
                    }
                    
                    return;
                }
                else if (hasVideoOrAudio && !medInfoExists)
                {
                    // 场景 2 有media info但没有.mediainfo: 创建.medinfo，进行备份
                    _logger.Info($"[EverMedia] EventListener: Opportunity backup detected for '{item.Name ?? item.Path}' (ID: {item.Id}). Attempting backup.");
                    await _everMediaService.BackupAsync(item);
                    _probeFailureTracker.TryRemove(item.Id, out _);    // ** 成功 **：在这里重置/移除计数器
                    return;
                }
                // 场景 3: 恢复失败 (即 FFProbe 失败了)
                else if (!hasVideoOrAudio && !medInfoExists)
                {
                    var now = DateTime.UtcNow;
                    
                    // int maxRetries = config.MaxProbeRetries;
                    // TimeSpan resetInterval = TimeSpan.FromMinutes(config.ProbeFailureResetMinutes);
                    int maxRetries = config.FailureConfig.MaxProbeRetries;
                    TimeSpan resetInterval = TimeSpan.FromMinutes(config.FailureConfig.ProbeFailureResetMinutes);
                    (int currentCount, DateTime lastAttempt) = _probeFailureTracker.GetValueOrDefault(itemId, (0, DateTime.MinValue));
                
                    // 重置熔断器，超过设置的重试次数，超过设定的时间间隔
                    if (currentCount >= maxRetries && (now - lastAttempt > resetInterval))
                    {
                        _logger.Info($"[EverMedia] EventListener: Reset interval ({config.FailureConfig.ProbeFailureResetMinutes}m) passed for '{item.Name ?? item.Path}'. Resetting failure count.");
                        currentCount = 0;
                    }
                
                    // 熔断检查
                    if (currentCount >= maxRetries)
                    {
                        _logger.Debug($"[EverMedia] EventListener: Item '{item.Name ?? item.Path}' has failed probing {maxRetries} times. Ignoring (Manual fix required).");
                        return; 
                    }
                
                    // 短期冷却检查 (防止 1 秒内快速连击)
                    TimeSpan timeSinceLast = now - lastAttempt;
                    
                    if (timeSinceLast < _shortTermRetryDelay)
                    {
                        _logger.Info($"[EverMedia] EventListener: Throttling retry for '{item.Name ?? item.Path}'. Waiting {(_shortTermRetryDelay - timeSinceLast).TotalSeconds:F1}s before next attempt...");
                        
                        // await 等待
                        await Task.Delay(_shortTermRetryDelay - timeSinceLast);
                        
                        // 等待结束后，更新 'now' 时间，以便记录准确的尝试时间
                        now = DateTime.UtcNow; 
                    }
                
                    // 执行尝试
                    currentCount++;
                    _logger.Info($"[EverMedia] EventListener: V/A info is lost and no .medinfo backup exists for '{item.Name ?? item.Path}'. Attempt {currentCount}/{maxRetries}. Triggering FFProbe.");
                
                    _probeFailureTracker.AddOrUpdate(itemId, (currentCount, now), (key, oldValue) => (currentCount, now));
                    
                    await TriggerFullProbeAsync(item);
                    return;
                }
                else
                {
                    // 场景 4: 一切正常
                    _logger.Debug($"[EverMedia] EventListener: Item is healthy '{item.Name ?? item.Path}' (ID: {item.Id}). (State: HasV/A={hasVideoOrAudio}, HasMedinfo={medInfoExists}). No action needed.");
                    _probeFailureTracker.TryRemove(item.Id, out _);    // ** 清理冷却 **：如果项目恢复正常，也移除它的冷却标记
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia] EventListener: Unhandled exception in OnItemUpdated: {ex.Message}");
            _logger.Debug($"Stack trace for OnItemUpdated exception: {ex}");
        }
    }

    /// <summary>
    /// 触发一次完整的远程媒体探测（FFProbe），用于获取音视频和字幕流。
    /// </summary>
    private async Task TriggerFullProbeAsync(BaseItem item)
    {
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
            _logger.Info($"[EverMedia] FFProbe refresh request sent for {item.Name ?? item.Path}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia] EventListener: Failed to trigger FFProbe for {item.Name}: {ex.Message}");
            _logger.Debug($"Stack trace for OnItemAdded exception: {ex}");
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var kvp in _debounceTokens)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        
        _debounceTokens.Clear();
        _probeFailureTracker.Clear();
        
        return ValueTask.CompletedTask;
    }
}