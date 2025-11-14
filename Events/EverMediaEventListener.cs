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
                _logger.Info($"[EverMedia] EventListener: ItemAdded event triggered for .strm file: {item.Name}");

                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                if (medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: .medinfo file found for added item: {item.Name}. Attempting quick restore.");
                    await _everMediaService.RestoreAsync(item);
                }
                else
                {
                    _logger.Info($"[EverMedia] EventListener: No .medinfo file found for added item: {item.Name}. Triggering FFProbe to fetch MediaInfo.");
                    await TriggerFullProbeAsync(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia] EventListener: Unhandled exception in OnItemAdded: {ex.Message}");
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
	            _logger.Debug($"[EverMedia:EverMediaEventListener] Plugin is disabled. Ignoring ItemUpdated event for {e.Item.Name}.");
	            return;
	        }

            if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"[EverMedia] EventListener: ItemUpdated event received for .strm file: {item.Name} (ID: {item.Id}).");

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
                // var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;
                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                // -----------------------------------------------------------
                // 场景 1: 歧义状态 (V/A 丢失, 但备份存在)
                // -----------------------------------------------------------
                if (!hasVideoOrAudio && medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Ambiguous state (V/A loss, medinfo exists) for '{item.Name ?? item.Path}' (ID: {item.Id}). Comparing sub counts...");
                    
                    int savedExternalCount = _everMediaService.GetSavedExternalSubCount(item);
                    int currentExternalCount = mediaStreams?.Count(s => s.Type == MediaStreamType.Subtitle && s.IsExternal) ?? 0;
                
                    _logger.Debug($"[EverMedia] EventListener: Counts for '{item.Name ?? item.Path}'. SavedInMedinfo: {savedExternalCount}, CurrentFromGetMediaStreams: {currentExternalCount}");
                
                    if (currentExternalCount != savedExternalCount)
                    {
                        // 场景 1a: 字幕变更
                        _logger.Info($"[EverMedia] EventListener: Subtitle count mismatch. Assuming subtitle change for '{item.Name ?? item.Path}'. Deleting stale .medinfo and triggering FFProbe.");
                        try
                        {
                            _fileSystem.DeleteFile(medInfoPath);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.Error($"[EverMedia] EventListener: Failed to delete .medinfo file at {medInfoPath}: {deleteEx.Message}");
                        }
                        await TriggerFullProbeAsync(item);
                    }
                    else
                    {
                        // 场景 1b: 常规自愈
                        _logger.Info($"[EverMedia] EventListener: Subtitle count matches for '{item.Name ?? item.Path}'. Performing fast self-heal restore.");
                        await _everMediaService.RestoreAsync(item);
                    }
                    return; // 已经处理，退出
                }

                // -----------------------------------------------------------
                // 场景 2: 机会性备份 (V/A 存在, 备份不存在)
                // -----------------------------------------------------------
                if (hasVideoOrAudio && !medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Opportunity backup detected for '{item.Name ?? item.Path}' (ID: {item.Id}). Attempting backup.");
                    await _everMediaService.BackupAsync(item);
                    return;
                }
                
                // -----------------------------------------------------------
                // 场景 3: 恢复失败 (V/A 丢失, 备份也不存在) <-- 你要的逻辑
                // -----------------------------------------------------------
                if (!hasVideoOrAudio && !medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: V/A info is lost and no .medinfo backup exists for '{item.Name ?? item.Path}' (ID: {item.Id}). Triggering FFProbe to repopulate.");
                    await TriggerFullProbeAsync(item);
                    return;
                }

                // -----------------------------------------------------------
                // 场景 4: 一切正常 (V/A 存在, 备份也存在)
                // -----------------------------------------------------------
                _logger.Debug($"[EverMedia] EventListener: Item is healthy '{item.Name ?? item.Path}' (ID: {item.Id}). (State: HasV/A={hasVideoOrAudio}, HasMedinfo={medInfoExists}). No action needed.");
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
            _logger.Info($"[EverMedia] EventListener: FFProbe triggered successfully for {item.Name}.");
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
        return ValueTask.CompletedTask;
    }
}