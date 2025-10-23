// Tasks/MediaInfoBootstrapTask.cs
using MediaBrowser.Controller.Library; // ILibraryManager
using MediaBrowser.Controller.Providers; // IProviderManager
using MediaBrowser.Model.Logging; // ILogger
using MediaBrowser.Model.Tasks; // IScheduledTask, TaskTriggerInfo
using System; // For Guid
using System.Collections.Generic; // For IEnumerable
using System.Threading; // For CancellationToken
using System.Threading.Tasks; // For Task

using EverMedia.Services; // 引入 MediaInfoService

namespace EverMedia.Tasks; // 使用命名空间组织代码

/// <summary>
/// 计划任务：扫描并持久化 .strm 文件的 MediaInfo。
/// 这是主动维护者，负责初始化和持续维护。
/// </summary>
public class MediaInfoBootstrapTask : IScheduledTask // 实现 IScheduledTask 接口
{
    // --- 依赖注入的私有字段 ---
    private readonly ILogger _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly MediaInfoService _mediaInfoService;

    // --- 构造函数：接收依赖项 ---
    public MediaInfoBootstrapTask(
        ILogger logger,                   // 用于记录任务执行日志
        ILibraryManager libraryManager,   // 用于查询媒体库项目
        IProviderManager providerManager, // 用于触发元数据刷新（探测）
        MediaInfoService mediaInfoService // 用于执行备份和恢复逻辑
    )
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _mediaInfoService = mediaInfoService;
    }

    // --- IScheduledTask 接口成员 ---

    public string Name => "MediaInfo Bootstrap Task"; // 任务在 UI 中显示的名称

    public string Key => "MediaInfoBootstrapTask"; // 任务的唯一键

    public string Description => "Scan and persist MediaInfo for .strm files."; // 任务描述

    public string Category => "Library"; // 任务所属类别

    // --- 获取默认触发器 ---
    // 返回一个 TaskTriggerInfo 对象的集合，定义任务的默认运行计划。
    // 如果返回空集合或 null，则任务默认不会自动运行，只能手动触发。
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // 示例：如果希望任务每天凌晨 2 点运行，可以这样配置：
        // yield return new TaskTriggerInfo
        // {
        //     Type = TaskTriggerInfo.TriggerDaily,
        //     TimeOfDayTicks = TimeSpan.FromHours(2).Ticks // 2 AM
        // };

        // 当前设置：无默认触发器，任务仅可手动运行。
        return Array.Empty<TaskTriggerInfo>(); // 返回空集合
    }

    // --- 核心执行方法 ---
    // ✅ 修正 1: 方法名从 ExecuteAsync 改为 Execute
    // ✅ 修正 2: 参数顺序从 (IProgress, CancellationToken) 改为 (CancellationToken, IProgress)
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("[MediaInfoBootstrapTask] Task execution started.");

        try
        {
            // TODO: 实现计划任务的核心逻辑
            // 1. 查询所有 .strm 文件
            // 2. 对每个 .strm 文件：
            //    a. 检查是否有 .medinfo 文件
            //       - 如果有，尝试恢复 (调用 _mediaInfoService.RestoreAsync)
            //    b. 如果没有，检查是否有 MediaStreams
            //       - 如果没有，触发探测 (调用 item.RefreshMetadata 或 providerManager.QueueRefresh)
            //         -> 探测成功后，ItemUpdated 事件会触发，由事件监听器处理备份
            // 3. 使用 progress.Report 更新进度 (注意参数顺序已变)
            // 4. 监听 cancellationToken.IsCancellationRequested

            _logger.Info("[MediaInfoBootstrapTask] Task execution completed (Not Implemented Yet).");
        }
        catch (OperationCanceledException)
        {
            // 任务被取消
            _logger.Info("[MediaInfoBootstrapTask] Task execution was cancelled.");
            throw; // 重新抛出以正确标记任务状态
        }
        catch (Exception ex)
        {
            // 任务执行出错
            _logger.Error($"[MediaInfoBootstrapTask] Task execution failed: {ex.Message}");
            _logger.Debug(ex.StackTrace); // 可选：记录详细堆栈
            throw; // 重新抛出以正确标记任务状态
        }
    }
}
