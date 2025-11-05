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
            _logger.Debug(ex, "Stack trace for OnItemAdded exception:");
        }
    }

    // --- ItemUpdated: 更新事件处理（含字幕刷新）---
    public async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        try
        {
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
                var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;

                string medInfoPath = _everMediaService.GetMedInfoPath(item);
                bool medInfoExists = _fileSystem.FileExists(medInfoPath);

                _logger.Debug($"[EverMedia] EventListener: Checking criteria for {item.Path}. HasVideoOrAudio: {hasVideoOrAudio}, HasSubtitles: {hasSubtitles}, MedInfoExists: {medInfoExists}");

                // ✅ 字幕-only 场景：删除 .medinfo + 触发 probe
                if (hasSubtitles && !hasVideoOrAudio && medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Detected subtitle-only update for {item.Path}. Deleting .medinfo and triggering FFProbe to refresh full MediaInfo.");
                    try
                    {
                        _fileSystem.DeleteFile(medInfoPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.Error($"[EverMedia] EventListener: Failed to delete .medinfo file at {medInfoPath}: {deleteEx.Message}");
                    }
                    await TriggerFullProbeAsync(item);
                    return;
                }

                // 原有逻辑：自愈
                if (!hasVideoOrAudio && medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Self-heal detected for item: {item.Path}. No MediaInfo, .medinfo exists. Attempting restore.");
                    await _everMediaService.RestoreAsync(item);
                }
                // 原有逻辑：机会性备份
                else if (hasVideoOrAudio && !medInfoExists)
                {
                    _logger.Info($"[EverMedia] EventListener: Opportunity backup detected for item: {item.Path}. MediaInfo exists, .medinfo missing. Attempting backup.");
                    await _everMediaService.BackupAsync(item);
                }
                else
                {
                    _logger.Debug($"[EverMedia] EventListener: ItemUpdated event for {item.Path} did not meet self-heal or backup criteria. HasVideoOrAudio: {hasVideoOrAudio}, MedInfoExists: {medInfoExists}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[EverMedia] EventListener: Unhandled exception in OnItemUpdated: {ex.Message}");
            _logger.Debug(ex, "Stack trace for OnItemUpdated exception:");
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
            _logger.Debug(ex, "Stack trace for FFProbe failure:");
        }
    }

    // ✅ 修正 DisposeAsync：移除 async，避免警告
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