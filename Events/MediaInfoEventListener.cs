// Events/MediaInfoEventListener.cs
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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
        // IServerApplicationHost applicationHost // 不再需要注入
    )
    {
        _logger = logger;
        _everMediaService = evermediaService;
        _fileSystem = fileSystem;
        // _applicationHost = applicationHost; // 不再赋值
    }

    // --- 事件处理方法：处理 ItemAdded 事件 ---
    public async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        // 注意：ItemAdded 事件可能传递多种类型的 BaseItem
        // 我们只关心 .strm 文件
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"[EverMediaEventListener] ItemAdded event triggered for .strm file: {item.Path}");

            // V6 架构: 快速恢复逻辑
            // 1. 检查是否存在 .medinfo 文件 (使用内部方法获取路径)
            // string medInfoPath = GetMedInfoPath(item);
            string medInfoPath = _everMediaService.GetMedInfoPath(item);
            if (_fileSystem.FileExists(medInfoPath))
            {
                _logger.Info($"[EverMediaEventListener] .medinfo file found for added item: {item.Path}. Attempting quick restore.");
                // 2. 如果存在，调用 RestoreAsync
                await _everMediaService.RestoreAsync(item);
            }
            else
            {
                _logger.Debug($"[EverMediaEventListener] No .medinfo file found for added item: {item.Path}. Delegating to bootstrap task.");
                // 3. 如果不存在，不执行任何操作（交给计划任务处理）
            }
        }
        // 如果不是 .strm 文件，不做任何操作
    }

    // --- 事件处理方法：处理 ItemUpdated 事件 ---
    public async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug($"[EverMediaEventListener] ItemUpdated event received for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count before debounce: {(item.MediaStreams?.Count ?? 0)}");

            // --- 防抖逻辑 ---
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

            // --- 获取流信息 ---
            var mediaStreams = item.GetMediaStreams();
            var hasVideoOrAudio = mediaStreams?.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio) == true;
            var hasSubtitles = mediaStreams?.Any(s => s.Type == MediaStreamType.Subtitle) == true;

            string medInfoPath = _everMediaService.GetMedInfoPath(item);
            bool medInfoExists = _fileSystem.FileExists(medInfoPath);

            _logger.Debug($"[EverMediaEventListener] Checking criteria for {item.Path}. HasVideoOrAudio: {hasVideoOrAudio}, HasSubtitles: {hasSubtitles}, MedInfoExists: {medInfoExists}");

            // ✅ 新增逻辑：检测到“只有字幕”时，删除 .medinfo 文件
            if (hasSubtitles && !hasVideoOrAudio && medInfoExists)
            {
                _logger.Info($"[EverMediaEventListener] Detected subtitle-only update for {item.Path}. Deleting .medinfo to allow future backup with new streams.");
                try
                {
                    _fileSystem.DeleteFile(medInfoPath);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[EverMediaEventListener] Failed to delete .medinfo file {medInfoPath}: {ex.Message}");
                }
                // 注意：此处不 restore，也不 backup，仅删除 .medinfo
                return;
            }

            // --- 原有自愈/备份逻辑 ---
            if (!hasVideoOrAudio && medInfoExists)
            {
                _logger.Info($"[EverMediaEventListener] Self-heal detected for item: {item.Path}. No MediaInfo, .medinfo exists. Attempting restore.");
                await _everMediaService.RestoreAsync(item);
            }
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

    // --- 辅助方法：检查项目是否拥有媒体信息 ---
    // 参考 StrmAssistant 的 HasMediaInfo 实现
    private bool HasMediaInfo(BaseItem item)
    {
        // 检查运行时间，这是媒体信息存在的一个强指标
        if (!item.RunTimeTicks.HasValue)
        {
            _logger.Debug($"[EverMediaEventListener] Item {item.Path} has no RunTimeTicks.");
            return false;
        }

        // 检查 GetMediaStreams() 的结果，寻找视频或音频流
        var mediaStreams = item.GetMediaStreams(); // 调用方法主动获取
        var hasVideoOrAudio = mediaStreams?.Any(i => i.Type == MediaStreamType.Video || i.Type == MediaStreamType.Audio) ?? false;

        if (!hasVideoOrAudio)
        {
            _logger.Debug($"[EverMediaEventListener] Item {item.Path} has RunTimeTicks but no Video or Audio streams via GetMediaStreams().");
        }
        else
        {
            _logger.Debug($"[EverMediaEventListener] Item {item.Path} has MediaInfo (RunTimeTicks and Video/Audio streams).");
        }

        return hasVideoOrAudio; // 可以根据需要决定是否包含 Size == 0 的检查
    }

    // --- Cleanup ---
    public async ValueTask DisposeAsync()
    {
        // Cancel any pending debounce timers
        foreach (var kvp in _debounceTokens)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _debounceTokens.Clear();
    }
}
