#nullable enable

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin
{
    /// <summary>
    /// 计划任务：从 .mediainfo 文件恢复媒体元数据
    /// </summary>
    public class MediaInfoRestoreTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly MediaInfoService _mediaInfoService;

        /// <summary>
        /// 构造函数 - Emby 4.9 会自动注入这些依赖
        /// </summary>
        public MediaInfoRestoreTask(
            ILibraryManager libraryManager,
            ILogger logger,
            IMediaSourceManager mediaSourceManager,
            IItemRepository itemRepository,
            IFileSystem fileSystem)
        {
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediaSourceManager = mediaSourceManager ?? throw new ArgumentNullException(nameof(mediaSourceManager));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            
            // 手动创建 MediaInfoService 实例
            _mediaInfoService = new MediaInfoService(
                logger,
                libraryManager,
                mediaSourceManager,
                itemRepository,
                fileSystem
            );
            
            _logger.Info("EmbyMedia: MediaInfoRestoreTask initialized");
        }

        /// <summary>
        /// 任务显示名称
        /// </summary>
        public string Name => "Restore MediaInfo from .mediainfo files";

        /// <summary>
        /// 任务唯一键
        /// </summary>
        public string Key => "EmbyMediaRestoreMediaInfo";

        /// <summary>
        /// 任务描述
        /// </summary>
        public string Description => "Restores technical metadata (MediaStreams, RunTimeTicks, etc.) for media items from .mediainfo backup files.";

        /// <summary>
        /// 任务分类
        /// </summary>
        public string Category => "EmbyMedia";

        /// <summary>
        /// 获取默认触发器（默认每天凌晨3点运行）
        /// </summary>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // 每天凌晨 3 点运行
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            };
        }

        /// <summary>
        /// 执行任务
        /// </summary>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("EmbyMedia: Restore Task started");

            try
            {
                // 获取所有视频和音频项目
                var items = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    MediaTypes = new[] { MediaType.Video, MediaType.Audio },
                    Recursive = true,
                    IsVirtualItem = false
                });

                if (items == null || items.Count == 0)
                {
                    _logger.Info("EmbyMedia: No media items found to process");
                    progress.Report(100);
                    return;
                }

                var totalItems = items.Count;
                var processed = 0;
                var restoredCount = 0;
                var errorCount = 0;

                _logger.Info("EmbyMedia: Found {0} items to check for MediaInfo restoration", totalItems);

                foreach (var item in items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Info("EmbyMedia: Restore Task cancelled by user");
                        break;
                    }

                    try
                    {
                        // 检查项目是否缺少关键元数据
                        var libraryOptions = _libraryManager.GetLibraryOptions(item);
                        var mediaSources = item.GetMediaSources(false, false, libraryOptions);
                        
                        var hasMediaInfo = item.RunTimeTicks.HasValue && 
                                          mediaSources != null &&
                                          mediaSources.Count > 0 && 
                                          mediaSources[0].MediaStreams != null &&
                                          mediaSources[0].MediaStreams.Count > 0;

                        if (!hasMediaInfo)
                        {
                            _logger.Debug("EmbyMedia: Item lacks MediaInfo, attempting restore: {0}", item.Path ?? "unknown");
                            
                            var restoreResult = await _mediaInfoService.RestoreMediaInfoAsync(item, cancellationToken);
                            
                            if (restoreResult)
                            {
                                restoredCount++;
                                _logger.Info("EmbyMedia: Successfully restored MediaInfo for {0}", item.Path ?? "unknown");
                            }
                            else
                            {
                                _logger.Debug("EmbyMedia: No backup found or restore failed for {0}", item.Path ?? "unknown");
                            }
                        }
                        else
                        {
                            _logger.Debug("EmbyMedia: Item already has MediaInfo, skipping: {0}", item.Path ?? "unknown");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info("EmbyMedia: Restore Task cancelled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.ErrorException("EmbyMedia: Error processing item {0}", ex, item.Path ?? "unknown");
                    }

                    processed++;
                    
                    // 更新进度
                    var percentComplete = (double)processed / totalItems * 100;
                    progress.Report(percentComplete);

                    // 每处理 100 个项目记录一次进度
                    if (processed % 100 == 0)
                    {
                        _logger.Info("EmbyMedia: Restore Task Progress: {0}/{1} items processed, {2} restored, {3} errors", 
                            processed, totalItems, restoredCount, errorCount);
                    }
                }

                _logger.Info("EmbyMedia: Restore Task completed. Processed: {0}, Restored: {1}, Errors: {2}", 
                    processed, restoredCount, errorCount);
                    
                progress.Report(100);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("EmbyMedia: Restore Task was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("EmbyMedia: Fatal error in Restore Task", ex);
                throw;
            }
        }
    }
}
