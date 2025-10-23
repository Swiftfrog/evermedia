// Events/MediaInfoEventListener.cs
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using System;
using EverMedia.Services; // 引入 MediaInfoService

namespace EverMedia.Events; // 使用命名空间组织代码

/// <summary>
/// 事件监听器：负责监听 Emby 的 ItemAdded 和 ItemUpdated 事件，
/// 并触发相应的 MediaInfoService 逻辑。
/// </summary>
public class MediaInfoEventListener
{
    // --- 依赖注入的私有字段 ---
    private readonly ILogger _logger;
    private readonly MediaInfoService _mediaInfoService;

    // --- 构造函数：接收依赖项 ---
    public MediaInfoEventListener(
        ILogger logger,           // 用于记录事件处理日志
        MediaInfoService mediaInfoService // 用于执行备份和恢复逻辑
    )
    {
        _logger = logger;
        _mediaInfoService = mediaInfoService;
    }

    // --- 事件处理方法：处理 ItemAdded 事件 ---
    public async void OnItemAdded(object sender, ItemChangeEventArgs e)
    {
        // 注意：ItemAdded 事件可能传递多种类型的 BaseItem
        // 我们只关心 .strm 文件
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"[MediaInfoEventListener] ItemAdded event triggered for .strm file: {item.Path}");

            // TODO: 实现快速恢复逻辑 (检查 .medinfo 文件是否存在，如果存在则恢复)
            // 例如: await _mediaInfoService.RestoreAsync(item);
            // 目前，我们只调用 RestoreAsync 来验证调用流程
            await _mediaInfoService.RestoreAsync(item);
        }
        // 如果不是 .strm 文件，不做任何操作
    }

    // --- 事件处理方法：处理 ItemUpdated 事件 ---
    public async void OnItemUpdated(object sender, ItemChangeEventArgs e)
    {
        // 同样，只关心 .strm 文件
        if (e.Item is BaseItem item && item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"[MediaInfoEventListener] ItemUpdated event triggered for .strm file: {item.Path}");

            // TODO: 实现自愈或备份逻辑 (检查 MediaInfo 是否丢失或是否首次获取到)
            // 例如: 检查 item.HasMediaStreams, 调用 await _mediaInfoService.BackupAsync(item) 或 RestoreAsync(item)
            // 目前，我们只调用 BackupAsync 来验证调用流程
            await _mediaInfoService.BackupAsync(item);
        }
        // 如果不是 .strm 文件，不做任何操作
    }
}
