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

namespace EverMedia.Events; // 使用命名空间组织代码

/// <summary>
/// 事件监听器：负责监听 Emby 的 ItemAdded 和 ItemUpdated 事件，
/// 并触发相应的 MediaInfoService 逻辑。
/// </summary>
public class MediaInfoEventListener : IAsyncDisposable // Implement IAsyncDisposable for proper cleanup of timers
{
    // --- 依赖注入的私有字段 ---
    private readonly ILogger _logger;
    private readonly MediaInfoService _mediaInfoService;
    private readonly IFileSystem _fileSystem; // 需要 IFileSystem 来检查 .medinfo 文件是否存在
    // private readonly IServerApplicationHost _applicationHost; // 不再注入此服务

    // --- 事件防抖相关 ---
    // 使用 ConcurrentDictionary 存储每个 Item 的 CancellationTokenSource
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _debounceTokens = new();

    // --- 构造函数：接收依赖项 ---
    public MediaInfoEventListener(
        ILogger logger,           // 用于记录事件处理日志
        MediaInfoService mediaInfoService, // 用于执行备份和恢复逻辑
        IFileSystem fileSystem // 用于检查 .medinfo 文件
        // IServerApplicationHost applicationHost // 不再需要注入
    )
    {
        _logger = logger;
        _mediaInfoService = mediaInfoService;
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
            _logger.Info($"[EverMedia:MediaInfoEventListener] ItemAdded event triggered for .strm file: {item.Path}");

            // V6 架构: 快速恢复逻辑
            // 1. 检查是否存在 .medinfo 文件 (使用内部方法获取路径)
            string medInfoPath = GetMedInfoPath(item);
            if (_fileSystem.FileExists(medInfoPath))
            {
                _logger.Info($"[EverMedia:MediaInfoEventListener] .medinfo file found for added item: {item.Path}. Attempting quick restore.");
                // 2. 如果存在，调用 RestoreAsync
                await _mediaInfoService.RestoreAsync(item);
            }
            else
            {
                _logger.Debug($"[EverMedia:MediaInfoEventListener] No .medinfo file found for added item: {item.Path}. Delegating to bootstrap task.");
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
                _logger.Debug($"[EverMedia:MediaInfoEventListener] ItemUpdated debounce cancelled for item: {item.Path}");
                return;
            }
            finally
            {
                // 从字典中移除 CTS 并释放资源
                _debounceTokens.TryRemove(itemId, out _);
                newCts.Dispose();
            }

            // 2. 更新原因过滤: 忽略播放开始/结束等事件
            // ItemChangeEventArgs 本身不直接包含原因，但可以通过其他方式推断或检查项目状态
            // 这里我们主要依赖于状态检查 (HasMediaStreams) 来区分是恢复还是备份
            // 播放事件通常不会改变 HasMediaStreams，所以这个检查本身就有一定的过滤作用
            // 如果需要更精确的过滤，可能需要更深入的 Emby 内部机制，暂时按状态检查

            // 3. 逻辑判断
            string medInfoPath = GetMedInfoPath(item); // 使用内部方法获取路径
            bool hasMediaInfo = item.HasMediaStreams; // 检查项目是否有 MediaStreams
            bool medInfoExists = _fileSystem.FileExists(medInfoPath);

            if (!hasMediaInfo && medInfoExists)
            {
                // 自愈逻辑: 数据库中没有 MediaInfo，但 .medinfo 文件存在
                _logger.Info($"[EverMedia:MediaInfoEventListener] Self-heal detected for item: {item.Path}. No MediaStreams, .medinfo exists. Attempting restore.");
                await _mediaInfoService.RestoreAsync(item);
            }
            else if (hasMediaInfo && !medInfoExists)
            {
                // 机会性备份逻辑: 数据库中有 MediaInfo，但 .medinfo 文件不存在
                _logger.Info($"[EverMedia:MediaInfoEventListener] Opportunity backup detected for item: {item.Path}. MediaStreams exist, .medinfo missing. Attempting backup.");
                await _mediaInfoService.BackupAsync(item);
            }
            else
            {
                // 其他情况：例如，都有或都无，或者更新与 MediaInfo 无关
                _logger.Debug($"[EverMedia:MediaInfoEventListener] ItemUpdated event for {item.Path} did not meet self-heal or backup criteria. HasMediaInfo: {hasMediaInfo}, MedInfoExists: {medInfoExists}");
            }
        }
        // 如果不是 .strm 文件，不做任何操作
    }

    // --- 辅助方法：获取插件配置 ---
    // ✅ 使用 Plugin.Instance.Configuration 模式，与 MediaInfoService 一致
    private PluginConfiguration? GetConfiguration()
    {
        // ✅ 通过 Plugin.Instance 获取配置
        return Plugin.Instance.Configuration;
    }

    // --- 辅助方法：生成 .medinfo 文件路径 ---
    // ✅ 直接复制并修改自 MediaInfoService.cs 的 GetMedInfoPath 方法，使用 Plugin.Instance 获取配置
    private string GetMedInfoPath(BaseItem item)
    {
        // ✅ 修正：检查 item.Path 是否为 null
        if (string.IsNullOrEmpty(item.Path))
        {
            _logger.Error($"[EverMedia:MediaInfoEventListener] Item path is null or empty for item ID: {item.Id}. Cannot generate MedInfo path.");
            // 返回一个默认路径或抛出异常，取决于你的处理策略
            // 这里我们返回一个基于 ID 的默认路径
            return Path.Combine(item.ContainingFolderPath, item.Id.ToString() + ".medinfo");
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        string medInfoFileName = fileNameWithoutExtension + ".medinfo";

        // ✅ 在方法内部获取当前配置 (调用内部的 GetConfiguration 方法，该方法使用 Plugin.Instance)
        var config = GetConfiguration();
        if (config == null)
        {
             _logger.Warn("[EverMedia:MediaInfoEventListener] Failed to get plugin configuration for GetMedInfoPath, using default SideBySide mode.");
             // 如果配置获取失败，返回 SideBySide 模式下的路径作为默认值
             return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }

        // 使用配置
        if (config.BackupMode == "Centralized" && !string.IsNullOrEmpty(config.CentralizedRootPath))
        {
            // 如果是中心化模式且路径有效，则构建中心化路径
            // 注意：GetRelativePath 可能需要处理不同的根目录情况
            string itemDir = Path.GetDirectoryName(item.Path) ?? item.ContainingFolderPath;
            string relativePath = Path.GetRelativePath(item.ContainingFolderPath, itemDir);
            // GetRelativePath 可能返回 "." 或 ".." 或包含 ".." 的路径，需要处理
            if (relativePath == ".")
            {
                relativePath = string.Empty; // 表示与 ContainingFolderPath 相同
            }
            else if (relativePath.StartsWith(".."))
            {
                // 如果相对路径向上跳出了 ContainingFolderPath，可能需要警告或特殊处理
                _logger.Warn($"[EverMedia:MediaInfoEventListener] Relative path calculation for centralized storage resulted in '{relativePath}' for item '{item.Path}'. Using SideBySide mode for this item.");
                 return Path.Combine(item.ContainingFolderPath, medInfoFileName);
            }
            return Path.Combine(config.CentralizedRootPath, relativePath, medInfoFileName);
        }
        else
        {
            // 默认：SideBySide 模式，.medinfo 文件与 .strm 文件同目录
            return Path.Combine(item.ContainingFolderPath, medInfoFileName);
        }
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
