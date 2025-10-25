// Tasks/MediaInfoBootstrapTask.cs (Alternative Rate Limiting using Task.Delay)
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

namespace EverMedia.Tasks; // 使用命名空间组织代码

/// <summary>
/// 计划任务：扫描并持久化 .strm 文件的 MediaInfo。
/// 这是主动维护者，负责初始化和持续维护。
/// </summary>
public class MediaInfoBootstrapTask : IScheduledTask // 实现 IScheduledTask 接口
{
    // ... (其他字段和构造函数保持不变) ...

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        _logger.Info("[MediaInfoBootstrapTask] Task execution started.");

        var config = Plugin.Instance.Configuration;
        if (config == null) { _logger.Error("[MediaInfoBootstrapTask] Failed to get plugin configuration. Cannot proceed."); return; }

        var taskStartTime = DateTime.UtcNow;

        try
        {
            // ... (查询逻辑保持不变) ...
            var strmItemsToProcess = allVideoItems.Where(item => item.Path != null && item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase)).ToList();

            _logger.Info($"[MediaInfoBootstrapTask] Found {strmItemsToProcess.Count} .strm files with metadata updated since last run to process.");

            var totalItems = strmItemsToProcess.Count;
            if (totalItems == 0) { _logger.Info("[MediaInfoBootstrapTask] No .strm files found with updated metadata since last run. Task completed."); progress?.Report(100); return; }

            var processedCount = 0;
            var restoredCount = 0;
            var probedCount = 0;
            var skippedCount = 0;

            // --- Rate Limiting: Simple delay-based approach ---
            // Configure desired rate (e.g., 1 request every 3 seconds)
            var rateLimitIntervalMs = 3000; // 3 seconds in milliseconds (configurable via PluginConfiguration if needed)
            var lastProbeStart = DateTimeOffset.MinValue; // Track the time the last probe started

            // --- Concurrency Control ---
            var maxConcurrency = config.MaxConcurrency > 0 ? config.MaxConcurrency : 1; // 确保至少为 1
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = new List<Task>();

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

            foreach (var item in strmItemsToProcess)
            {
                if (cancellationToken.IsCancellationRequested) break;

                await semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        // --- Simple Rate Limiting Logic ---
                        // Wait until the required interval has passed since the last probe started
                        var now = DateTimeOffset.UtcNow;
                        var timeToWait = rateLimitIntervalMs - (now - lastProbeStart).TotalMilliseconds;
                        if (timeToWait > 0)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(timeToWait), cancellationToken);
                        }
                        // Update the timestamp *after* the delay
                        lastProbeStart = DateTimeOffset.UtcNow;
                        // --- End of Rate Limiting Logic ---

                        _logger.Debug($"[MediaInfoBootstrapTask] Processing .strm file: {item.Path} (DateLastSaved: {item.DateLastSaved:O})");

                        string medInfoPath = _mediaInfoService.GetMedInfoPath(item);

                        if (System.IO.File.Exists(medInfoPath))
                        {
                            _logger.Info($"[MediaInfoBootstrapTask] Found .medinfo file for {item.Path}. Attempting restore.");
                            var restoreResult = await _mediaInfoService.RestoreAsync(item);
                            if (restoreResult) { restoredCount++; }
                            else { _logger.Warn($"[MediaInfoBootstrapTask] Failed to restore MediaInfo for {item.Path}."); }
                        }
                        else
                        {
                            _logger.Debug($"[MediaInfoBootstrapTask] No .medinfo file found for {item.Path}.");
                            bool hasMediaInfo = item.GetMediaStreams()?.Any(i => i.Type == MediaStreamType.Video || i.Type == MediaStreamType.Audio) ?? false;

                            if (!hasMediaInfo)
                            {
                                _logger.Info($"[MediaInfoBootstrapTask] No MediaInfo found for {item.Path} and no .medinfo file. Attempting probe.");
                                await item.RefreshMetadata(refreshOptions, cancellationToken);
                                probedCount++;
                                _logger.Info($"[MediaInfoBootstrapTask] Probe initiated for {item.Path}. Event listener will handle backup if successful.");
                            }
                            else
                            {
                                _logger.Debug($"[MediaInfoBootstrapTask] MediaInfo exists for {item.Path} but no .medinfo file. Event listener may handle backup.");
                                skippedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[MediaInfoBootstrapTask] Error processing item {item.Path}: {ex.Message}");
                        _logger.Debug(ex.StackTrace);
                    }
                    finally
                    {
                        semaphore.Release(); // Release the concurrency semaphore

                        Interlocked.Increment(ref processedCount);
                        var currentProgress = (double)processedCount / totalItems * 100.0;
                        progress?.Report(currentProgress);
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            var totalProcessed = restoredCount + probedCount + skippedCount;
            _logger.Info($"[MediaInfoBootstrapTask] Task execution completed. Total .strm files processed: {totalProcessed}. Breakdown -> Restored from .medinfo: {restoredCount}, Probed for new meta {probedCount}, Skipped (already has metadata): {skippedCount}.");

            Plugin.Instance.UpdateLastBootstrapTaskRun(taskStartTime);
            _logger.Info($"[MediaInfoBootstrapTask] Last run timestamp updated to {taskStartTime:O} via Plugin.Instance.");

        }
        catch (OperationCanceledException)
        {
            _logger.Info("[MediaInfoBootstrapTask] Task execution was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"[MediaInfoBootstrapTask] Task execution failed: {ex.Message}");
            _logger.Debug(ex.StackTrace);
            throw;
        }
    }
}
