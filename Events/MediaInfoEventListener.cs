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
        // 同样，只关心 .strm 文件
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            // V6 架构: 自愈与备份逻辑 (带防抖和原因过滤)

            // --- 添加调试日志 ---
            _logger.Debug($"[EverMediaEventListener] ItemUpdated event received for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count before debounce: {(item.MediaStreams?.Count ?? 0)}");

            // 1. 事件防抖: 确保对同一 itemId 在 1 秒内最多处理一次
            var itemId = item.Id;
            if (_debounceTokens.TryGetValue(itemId, out var existingCts))
            {
                // 如果已有定时器，取消它
                existingCts.Cancel();
                existingCts.Dispose(); // 释放旧的 CTS
            }

            var newCts = new CancellationTokenSource();
            _debounceTokens[itemId] = newCts; // 存储新的 CTS

            try
            {
                // 等待 1 秒，如果被取消则不执行后续逻辑
                await Task.Delay(TimeSpan.FromSeconds(1), newCts.Token);
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，直接返回，不执行恢复或备份逻辑
                _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce cancelled for item: {item.Path}");
                return;
            }
            finally
            {
                // 从字典中移除 CTS 并释放资源
                _debounceTokens.TryRemove(itemId, out _);
                newCts.Dispose();
            }

            // --- 添加调试日志 ---
            _logger.Debug($"[EverMediaEventListener] ItemUpdated debounce completed for .strm file: {item.Path} (ID: {item.Id}). MediaStreams count after debounce: {(item.MediaStreams?.Count ?? 0)}");

            // 2. 更新原因过滤: 忽略播放开始/结束等事件
            // ItemChangeEventArgs 本身不直接包含原因，但可以通过其他方式推断或检查项目状态
            // 这里我们主要依赖于状态检查 (HasMediaInfo) 来区分是恢复还是备份
            // 播放事件通常不会改变 HasMediaInfo，所以这个检查本身就有一定的过滤作用
            // 如果需要更精确的过滤，可能需要更深入的 Emby 内部机制，暂时按状态检查

            // 3. 逻辑判断
            // string medInfoPath = GetMedInfoPath(item); // 使用内部方法获取路径
            string medInfoPath = _everMediaService.GetMedInfoPath(item);
            // --- 修正：使用新的 HasMediaInfo 方法 ---
            bool hasMediaInfo = HasMediaInfo(item);
            bool medInfoExists = _fileSystem.FileExists(medInfoPath);

            // --- 添加调试日志 ---
            _logger.Debug($"[EverMediaEventListener] Checking criteria for {item.Path}. HasMediaInfo (revised): {hasMediaInfo}, MedInfoExists: {medInfoExists}");

            if (!hasMediaInfo && medInfoExists)
            {
                // 自愈逻辑: 数据库中没有 MediaInfo，但 .medinfo 文件存在
                _logger.Info($"[EverMediaEventListener] Self-heal detected for item: {item.Path}. No MediaInfo, .medinfo exists. Attempting restore.");
                await _everMediaService.RestoreAsync(item);
            }
            else if (hasMediaInfo && !medInfoExists)
            {
                // 机会性备份逻辑: 数据库中有 MediaInfo，但 .medinfo 文件不存在
                _logger.Info($"[EverMediaEventListener] Opportunity backup detected for item: {item.Path}. MediaInfo exists, .medinfo missing. Attempting backup.");
                await _everMediaService.BackupAsync(item);
            }
            else
            {
                // 其他情况：例如，都有或都无，或者更新与 MediaInfo 无关
                _logger.Debug($"[EverMediaEventListener] ItemUpdated event for {item.Path} did not meet self-heal or backup criteria. HasMediaInfo (revised): {hasMediaInfo}, MedInfoExists: {medInfoExists}");
            }
        }
        // 如果不是 .strm 文件，不做任何操作
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
