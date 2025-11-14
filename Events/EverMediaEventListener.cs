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
	            _logger.Debug($"[EverMedia] EventListener: Plugin is disabled. Ignoring ItemAdded event for {e.Item.Path}.");
	            return;
	        }
            
            if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"[EverMedia] EventListener: ItemAdded event triggered for .strm file: {item.Path}");

                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                if (medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: .medinfo file found for added item: {item.Path}. Attempting quick restore.");
                    await _everMediaService.RestoreAsync(item);
                }
                else
                {
                    _logger.Info($"[EverMedia] EventListener: No .medinfo file found for added item: {item.Path}. Triggering FFProbe to fetch MediaInfo.");
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
	            _logger.Debug($"[EverMedia:EverMediaEventListener] Plugin is disabled. Ignoring ItemUpdated event for {e.Item.Path}.");
	            return;
	        }

            if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"[EverMedia] EventListener: ItemUpdated event received for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count before debounce: {(item.MediaStreams?.Count ?? 0)}");

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
                    _logger.Debug($"[EverMedia] EventListener: ItemUpdated debounce cancelled for item: {item.Path}");
                    return;
                }
                finally
                {
                    _debounceTokens.TryRemove(itemId, out _);
                    newCts.Dispose();
                }

                _logger.Debug($"[EverMedia] EventListener: ItemUpdated debounce completed for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count after debounce: {(item.MediaStreams?.Count ?? 0)}");

                var mediaStreams = item.GetMediaStreams();
                var hasVideoOrAudio = mediaStreams?.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio) == true;
                // var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;
                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                // _logger.Debug($"[EverMedia] EventListener: Checking criteria for {item.Path}. HasVideoOrAudio: {hasVideoOrAudio}, HasSubtitles: {hasSubtitles}, MedInfoExists: {medInfoExists}");
                // -----------------------------------------------------------
                // 场景 1: V/A 丢失，且备份存在 (歧义状态)
                // -----------------------------------------------------------
                if (!hasVideoOrAudio && medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Ambiguous state detected (V/A loss with backup) for {item.Path}.");
                    
                    // 1. 从 .medinfo 文件中读取保存的计数
                    int savedExternalCount = _everMediaService.GetSavedExternalSubCount(item);

                    // 2. 从 Emby 的 (可信的) API 中获取 *当前* 的外挂字幕数量
                    int currentExternalCount = mediaStreams
                        .Count(s => s.Type == MediaStreamType.Subtitle && s.IsExternal);

                    _logger.Debug($"[EverMedia] EventListener: Comparing counts... SavedInMedinfo: {savedExternalCount}, CurrentFromGetMediaStreams: {currentExternalCount}");

                    // 3. 比较!
                    if (currentExternalCount != savedExternalCount)
                    {
                        // **场景: 字幕变更 (添加或删除)**
                        _logger.Info($"[EverMedia] EventListener: Subtitle count mismatch. Assuming subtitle change. Deleting stale .medinfo and triggering FFProbe.");
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
                        // **场景: 常规自愈**
                        _logger.Info($"[EverMedia] EventListener: Subtitle count matches. Performing fast self-heal restore.");
                        await _everMediaService.RestoreAsync(item);
                    }
                    return; // 已经处理，退出
                }

                // -----------------------------------------------------------
                // 场景 2: 机会性备份
                // -----------------------------------------------------------
                if (hasVideoOrAudio && !medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Opportunity backup detected for item: {item.Path}. MediaInfo exists, .medinfo missing. Attempting backup.");
                    await _everMediaService.BackupAsync(item);
                }
                // -----------------------------------------------------------
                // 场景 3: 一切正常
                // -----------------------------------------------------------
                else
                {
                    _logger.Debug($"[EverMedia] EventListener: ItemUpdated event for {item.Path} did not meet action criteria. (State: HasV/A={hasVideoOrAudio}, HasMedinfo={medInfoExists})");
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
            _logger.Info($"[EverMedia] EventListener: FFProbe triggered successfully for {item.Path}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia] EventListener: Failed to trigger FFProbe for {item.Path}: {ex.Message}");
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