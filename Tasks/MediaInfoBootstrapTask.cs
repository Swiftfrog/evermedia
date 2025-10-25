// Tasks/MediaInfoBootstrapTask.cs
using MediaBrowser.Controller.Entities; // BaseItem
using MediaBrowser.Controller.Library; // ILibraryManager
using MediaBrowser.Controller.Providers; // IProviderManager
using MediaBrowser.Model.Entities; // LocationType
using MediaBrowser.Model.Logging; // ILogger
using MediaBrowser.Model.Tasks; // IScheduledTask, TaskTriggerInfo
using System; // For Guid
using System.Collections.Generic; // For IEnumerable
using System.Threading; // For CancellationToken
using System.Threading.Tasks; // For Task
using EverMedia.Services; // 引入 MediaInfoService
using EverMedia.Configuration; // 引入配置类
using System.Linq; // For Where, Any
using MediaBrowser.Model.IO; // For IFileSystem, DirectoryService
// --- Add RateLimiting ---
using System.Threading.RateLimiting; // Add this using statement

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
    private readonly IFileSystem _fileSystem; // 注入 IFileSystem

    // --- 构造函数：接收依赖项 ---
    public MediaInfoBootstrapTask(
        ILogManager logManager,           // 请求日志管理器工厂
        ILibraryManager libraryManager,   // 用于查询媒体库项目
        IProviderManager providerManager, // 用于触发元数据刷新（探测）
        MediaInfoService mediaInfoService, // 用于执行备份和恢复逻辑
        IFileSystem fileSystem           // 用于 MetadataRefreshOptions
    )
    {
        // ✅ 使用 logManager 为这个特定的类创建一个 logger 实例
        _logger = logManager.GetLogger(GetType().Name);
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _mediaInfoService = mediaInfoService;
        _fileSystem = fileSystem; // 保存注入的 IFileSystem
    }

    // --- IScheduledTask 接口成员 ---

    public string Name => "MediaInfo Bootstrap Task"; // 任务在 UI 中显示的名称

    public string Key => "MediaInfoBootstrapTask"; // 任务的唯一键

    public string Description => "Scan and persist MediaInfo for .strm files."; // 任务描述

    public string Category => "EverMedia"; // 任务所属类别

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

        // 获取插件配置
        var config = Plugin.Instance.Configuration;
        if (config == null)
        {
            _logger.Error("[MediaInfoBootstrapTask] Failed to get plugin configuration. Cannot proceed.");
            return; // 配置获取失败，退出任务
        }

        // 记录任务开始时间，用于后续更新配置和查询
        var taskStartTime = DateTime.UtcNow;

        try
        {
            // 1. 智能扫描：高效查询库中所有可能的 .strm 文件
            // 使用 MinDateLastSaved 实现增量更新
            var lastRunTimestamp = config.LastBootstrapTaskRun;
            _logger.Info($"[MediaInfoBootstrapTask] Querying library for .strm files with metadata updated since {lastRunTimestamp?.ToString("O") ?? "the beginning of time"}...");

            var query = new InternalItemsQuery
            {
                // .strm 文件在 Emby 中被识别为视频类型。这是最有效的数据库索引过滤条件。
                MediaTypes = new[] { MediaType.Video },

                // 确保返回的项目都有一个文件系统路径，这是处理 .strm 文件的先决条件。
                HasPath = true,

                // 至关重要：确保查询能深入媒体库的所有子文件夹，以找到所有 .strm 文件。
                Recursive = true,

                // ✅ 新增：只查询自上次运行后元数据被保存过的项目
                MinDateLastSaved = lastRunTimestamp
            };

            var allVideoItems = _libraryManager.GetItemList(query);

            // 过滤出 Path 以 .strm 结尾的项目
            var strmItemsToProcess = allVideoItems.Where(item => item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase)).ToList();

            _logger.Info($"[MediaInfoBootstrapTask] Found {strmItemsToProcess.Count} .strm files with metadata updated since last run to process.");

            // 计算总进度 (基于过滤后的列表)
            var totalItems = strmItemsToProcess.Count; // List<T> 使用 .Count 属性
            if (totalItems == 0)
            {
                _logger.Info("[MediaInfoBootstrapTask] No .strm files found with updated metadata since last run. Task completed.");
                progress?.Report(100); // 报告 100% 进度
                return;
            }

            var processedCount = 0;
            var restoredCount = 0;
            var probedCount = 0;
            var skippedCount = 0;

            // 配置中的并发数限制
            var maxConcurrency = config.MaxConcurrency > 0 ? config.MaxConcurrency : 1; // 确保至少为 1
            // 配置中的速率限制参数 (例如，每秒最多 1 个请求，突发允许 2 个)
            // 你可以根据需要从配置中读取这些值，这里使用硬编码示例
            var permitsPerPeriod = 1; // 每个周期发放的许可数 (相当于每秒请求数)
            var tokenLimit = Math.Max(2, maxConcurrency); // 令牌桶的最大容量 (突发容量)
            var period = TimeSpan.FromSeconds(1); // 补充 permitsPerPeriod 个许可的时间间隔 (1秒)

            // 使用 SemaphoreSlim 限制并发数 (同时运行的 FFProbe 数量)
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            // 使用 RateLimiter 限制启动 FFProbe 的频率
            // 使用 TokenBucketRateLimiterOptions 的标准构造函数 (tokenLimit, period, permitsPerPeriod, queueLimitingStrategy)
            using var rateLimiter = new TokenBucketRateLimiter(
                new TokenBucketRateLimiterOptions(
                    tokenLimit, // tokenLimit: 桶的最大容量
                    period,     // tokenReplenishmentPeriod: 补充周期
                    permitsPerPeriod, // tokensPerPeriod: 每个周期补充的许可数
                    QueueLimitingStrategy.Wait // queueLimitingStrategy: 队列策略，默认为 Wait
                    // 注意：QueueLimitingStrategy 是枚举，需要导入 System.Threading.RateLimiting
                )
            );

            // 使用自定义并发控制
            var tasks = new List<Task>();

            // 使用注入的 IFileSystem 创建 DirectoryService，并配置 MetadataRefreshOptions
            var directoryService = new DirectoryService(_logger, _fileSystem);
            var refreshOptions = new MetadataRefreshOptions(directoryService)
            {
                EnableRemoteContentProbe = true,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                ReplaceAllMetadata = false, // 不替换其他元数据
                ImageRefreshMode = MetadataRefreshMode.ValidationOnly, // 不强制刷新图片
                ReplaceAllImages = false,
                EnableThumbnailImageExtraction = false, // 不提取缩略图
                EnableSubtitleDownloading = false // 不下载字幕
            };

            foreach (var item in strmItemsToProcess) // 注意：循环对象改为过滤后的列表
            {
                // 检查取消令牌
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("[MediaInfoBootstrapTask] Task execution was cancelled during processing.");
                    break; // 退出循环
                }

                // 等待并发信号量，控制同时运行的探测数
                await semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    // --- Rate Limiting: Acquire permit before starting FFProbe ---
                    RateLimitLease lease = null;
                    try
                    {
                        // 尝试获取速率限制许可，使用与任务相同的取消令牌
                        // TokenBucket 默认行为是等待直到有许可可用 (QueueLimitingStrategy.Wait)
                        lease = await rateLimiter.AcquireAsync(1, cancellationToken);
                        // 检查 IsAcquired 以确认是否成功获取，虽然 Wait 策略下通常会获取成功（除非被取消）
                        if (!lease.IsAcquired)
                        {
                            // This shouldn't happen with TokenBucket and Wait strategy unless explicitly cancelled or Reject is used.
                            _logger.Warn($"[MediaInfoBootstrapTask] Rate limit lease not acquired for {item.Path}. Skipping.");
                            return; // Exit this task instance without processing
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // AcquireAsync was cancelled, release semaphore and return
                        _logger.Debug($"[MediaInfoBootstrapTask] Rate limit acquisition cancelled for {item.Path}.");
                        semaphore.Release(); // Important: Release the semaphore before returning
                        return;
                    }
                    // --- End of Rate Limiting ---

                    try
                    {
                        // 检查取消令牌（在获取到速率限制许可后再次检查）
                        if (cancellationToken.IsCancellationRequested) return;

                        _logger.Debug($"[MediaInfoBootstrapTask] Processing .strm file: {item.Path} (DateLastSaved: {item.DateLastSaved:O})");

                        // 检查是否存在 .medinfo 文件
                        string medInfoPath = _mediaInfoService.GetMedInfoPath(item); // 直接调用 MediaInfoService 的公共方法

                        if (System.IO.File.Exists(medInfoPath))
                        {
                            _logger.Info($"[MediaInfoBootstrapTask] Found .medinfo file for {item.Path}. Attempting restore.");
                            // 存在 .medinfo 文件：尝试恢复 (自愈)
                            var restoreResult = await _mediaInfoService.RestoreAsync(item);
                            if (restoreResult)
                            {
                                restoredCount++;
                                _logger.Info($"[MediaInfoBootstrapTask] Successfully restored MediaInfo for {item.Path}.");
                            }
                            else
                            {
                                _logger.Warn($"[MediaInfoBootstrapTask] Failed to restore MediaInfo for {item.Path}.");
                            }
                        }
                        else
                        {
                            _logger.Debug($"[MediaInfoBootstrapTask] No .medinfo file found for {item.Path}.");
                            // 不存在 .medinfo 文件：检查是否已有 MediaStreams
                            // 使用 item.GetMediaStreams() 来获取最新状态，参考 MediaInfoEventListener
                            bool hasMediaInfo = item.GetMediaStreams()?.Any(i => i.Type == MediaStreamType.Video || i.Type == MediaStreamType.Audio) ?? false;

                            if (!hasMediaInfo)
                            {
                                _logger.Info($"[MediaInfoBootstrapTask] No MediaInfo found for {item.Path} and no .medinfo file. Attempting probe.");
                                // 没有 MediaStreams 且没有 .medinfo 文件：触发探测
                                // 使用预先创建的 MetadataRefreshOptions 来触发探测

                                // 调用 RefreshMetadata 来触发探测
                                await item.RefreshMetadata(refreshOptions, cancellationToken);
                                // 探测成功后，ItemUpdated 事件会被触发，EventListener 会处理备份
                                probedCount++;
                                _logger.Info($"[MediaInfoBootstrapTask] Probe initiated for {item.Path}. Event listener will handle backup if successful.");
                            }
                            else
                            {
                                // 有 MediaStreams 但没有 .medinfo 文件：可能是一个新添加的、有信息但未备份的项目
                                // 计划任务不直接处理这种情况，EventListener 会处理
                                _logger.Debug($"[MediaInfoBootstrapTask] MediaInfo exists for {item.Path} but no .medinfo file. Event listener may handle backup.");
                                skippedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[MediaInfoBootstrapTask] Error processing item {item.Path}: {ex.Message}");
                        _logger.Debug(ex.StackTrace); // 记录详细堆栈
                    }
                    finally
                    {
                        // 释放速率限制许可 (通过 using lease 或显式调用)
                        // TokenBucketRateLimiter's lease is typically disposed after use.
                        lease?.Dispose(); // Ensure the lease is returned to the limiter

                        // 释放并发信号量
                        semaphore.Release();

                        // 更新进度
                        Interlocked.Increment(ref processedCount);
                        var currentProgress = (double)processedCount / totalItems * 100.0;
                        progress?.Report(currentProgress);
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            // 等待所有任务完成
            await Task.WhenAll(tasks);

            // 优化日志输出
            var totalProcessed = restoredCount + probedCount + skippedCount;
            _logger.Info($"[MediaInfoBootstrapTask] Task execution completed. Total .strm files processed: {totalProcessed}. Breakdown -> Restored from .medinfo: {restoredCount}, Probed for new meta {probedCount}, Skipped (already has metadata): {skippedCount}.");

            // ✅ 在任务成功完成（没有被取消或抛出未处理异常）后，更新配置中的时间戳
            // 调用 Plugin.Instance 上的公共方法来更新和保存配置
            Plugin.Instance.UpdateLastBootstrapTaskRun(taskStartTime);
            _logger.Info($"[MediaInfoBootstrapTask] Last run timestamp updated to {taskStartTime:O} via Plugin.Instance.");

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
