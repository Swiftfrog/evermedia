#nullable enable

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging; // ILogger
using MediaBrowser.Model.MediaInfo; // MediaProtocol
using MediaBrowser.Model.Dlna; // SubtitleDeliveryMethod
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyMedia.Plugin;

// Interface definition remains the same, but implementation uses Emby's ILogger
public interface IMediaInfoService
{
    Task<bool> EnsureStrmMediaInfoAsync(BaseItem item, CancellationToken cancellationToken);
    Task<bool> BackupMediaInfoAsync(BaseItem item, CancellationToken cancellationToken);
    Task<bool> RestoreMediaInfoAsync(BaseItem item, CancellationToken cancellationToken);
}

public class MediaInfoService // Note: Not injected directly via constructor in this example, see Provider/Task
{
    private readonly ILogger _logger; // 使用 Emby 的非泛型 ILogger
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly IItemRepository _itemRepository;
    private readonly IFileSystem _fileSystem;

    public MediaInfoService(ILogger logger, ILibraryManager libraryManager, IMediaSourceManager mediaSourceManager, IItemRepository itemRepository, IFileSystem fileSystem)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _mediaSourceManager = mediaSourceManager;
        _itemRepository = itemRepository;
        _fileSystem = fileSystem;
    }

    // --- Implementation methods using Emby's ILogger ---
    // (The body of these methods remains largely the same, just ensure _logger calls use Emby's ILogger methods)

    public async Task<bool> EnsureStrmMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (item.Path == null || !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug("Item {0} is not a STRM file, skipping probe.", item.Path);
            return false; // Not a STRM file
        }

        var currentMediaSources = item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item));
        if (currentMediaSources.Count > 0 && currentMediaSources[0].MediaStreams.Count > 0)
        {
            _logger.Debug("STRM file {0} already has MediaStreams, skipping probe.", item.Path);
            return false; // Already has streams
        }

        _logger.Debug("Probing STRM file {0} for MediaInfo.", item.Path);
        try
        {
            // This call triggers the probe for STRM files
            var options = _libraryManager.GetLibraryOptions(item);
            var updatedSources = await Task.Run(() => _mediaSourceManager.GetStaticMediaSources(
                item,
                enableAlternateMediaSources: false,
                enablePathSubstitution: false,
                fillChapters: false,
                collectionFolders: Array.Empty<BaseItem>(),
                libraryOptions: options,
                deviceProfile: null,
                user: null
            ), cancellationToken);

            // Check if the probe was successful and streams were populated
            if (updatedSources.Count > 0 && updatedSources[0].MediaStreams.Count > 0)
            {
                _logger.Info("Successfully probed STRM file {0}, found {1} streams.", item.Path, updatedSources[0].MediaStreams.Count);
                return true;
            }
            else
            {
                _logger.Warn("Probing STRM file {0} did not yield any MediaStreams.", item.Path);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Error probing STRM file {0}: {1}", ex, item.Path, ex.Message); // 注意：Emby ILogger 可能有 ErrorException 方法
            return false;
        }
    }

    public async Task<bool> BackupMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var mediaSources = item.GetMediaSources(false, false, _libraryManager.GetLibraryOptions(item));
        if (mediaSources.Count == 0)
        {
            _logger.Debug("No MediaSources found for item {0}, skipping backup.", item.Path);
            return false;
        }

        var mediaInfoToSave = new List<SerializableMediaSourceInfo>();
        foreach (var source in mediaSources)
        {
            // Clean the MediaSourceInfo before serialization
            var cleanedSource = new SerializableMediaSourceInfo
            {
                Container = source.Container,
                RunTimeTicks = source.RunTimeTicks,
                Size = source.Size, // This is long?
                Bitrate = source.Bitrate,
                MediaStreams = new List<SerializableMediaStream>(),
                // Copy other necessary properties, excluding Emby internal IDs
                Name = source.Name,
                Protocol = source.Protocol,
                IsRemote = source.IsRemote,
                SupportsDirectPlay = source.SupportsDirectPlay,
                SupportsDirectStream = source.SupportsDirectStream,
                SupportsTranscoding = source.SupportsTranscoding,
                ContainerStartTimeTicks = source.ContainerStartTimeTicks // This is long?
                // Add other properties as needed, but avoid Id, ItemId, ServerId, Path, etc.
            };

            foreach (var stream in source.MediaStreams)
            {
                // Clean the MediaStream before serialization
                var cleanedStream = new SerializableMediaStream
                {
                    Type = stream.Type,
                    Codec = stream.Codec,
                    Language = stream.Language,
                    Channels = stream.Channels,
                    BitRate = stream.BitRate,
                    BitDepth = stream.BitDepth,
                    SampleRate = stream.SampleRate,
                    Height = stream.Height,
                    Width = stream.Width,
                    AverageFrameRate = stream.AverageFrameRate,
                    RealFrameRate = stream.RealFrameRate,
                    Profile = stream.Profile,
                    Level = stream.Level,
                    IsDefault = stream.IsDefault,
                    IsForced = stream.IsForced,
                    IsInterlaced = stream.IsInterlaced,
                    // IsAVC = stream.IsAVC, // REMOVED: Obsolete property
                    Comment = stream.Comment, // First and only assignment of Comment
                    TimeBase = stream.TimeBase,
                    CodecTag = stream.CodecTag,
                    // Add other properties as needed, but avoid Id, ItemId, ServerId, Path, etc.
                    // Note: Some properties like Extradata, NalLengthSize, PixelFormat, ColorSpace, etc., might be important depending on your needs
                    Extradata = stream.Extradata,
                    NalLengthSize = stream.NalLengthSize,
                    PixelFormat = stream.PixelFormat,
                    ColorSpace = stream.ColorSpace,
                    ColorTransfer = stream.ColorTransfer,
                    ColorPrimaries = stream.ColorPrimaries,
                    // --- REMOVED: Properties not in MediaStream ---
                    // DvVersionMajor = stream.DvVersionMajor,
                    // DvVersionMinor = stream.DvVersionMinor,
                    // DvProfile = stream.DvProfile,
                    // DvLevel = stream.DvLevel,
                    // RpuPresentFlag = stream.RpuPresentFlag,
                    // ElPresentFlag = stream.ElPresentFlag,
                    // BlPresentFlag = stream.BlPresentFlag,
                    // DvBlSignalCompatibilityId = stream.DvBlSignalCompatibilityId,
                    Title = stream.Title,
                    VideoRange = stream.VideoRange, // Read only, but safe to read for backup
                    // VideoRangeType = stream.VideoRangeType, // Not a property of MediaStream
                    // VideoDoViTitle = stream.VideoDoViTitle, // Not a property of MediaStream
                    RefFrames = stream.RefFrames,
                    // PacketLength = stream.PacketLength, // Not a property of MediaStream
                    ChannelLayout = stream.ChannelLayout,
                    IsAnamorphic = stream.IsAnamorphic,
                    AspectRatio = stream.AspectRatio,
                    Index = stream.Index,
                    // Score = stream.Score, // Not a property of MediaStream
                    IsExternal = stream.IsExternal,
                    DeliveryMethod = stream.DeliveryMethod,
                    DeliveryUrl = stream.DeliveryUrl,
                    IsExternalUrl = stream.IsExternalUrl,
                    // Note: Path, IsDefault, IsForced, IsInterlaced, IsTextSubtitleStream, SupportsExternalStream, etc. are often important
                    // but Path should be excluded from backup. IsDefault, IsForced might be okay if they are user-set.
                    Path = null, // Explicitly exclude Path
                    Id = null,   // Exclude Emby internal ID
                    ItemId = null, // Exclude Emby internal ID
                    ServerId = null // Exclude Emby internal ID
                };
                cleanedSource.MediaStreams.Add(cleanedStream);
            }
            mediaInfoToSave.Add(cleanedSource);
        }

        var jsonPath = GetMediaInfoJsonPath(item);
        var directory = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(mediaInfoToSave, options), cancellationToken);
            _logger.Info("MediaInfo backup successful for {0} to {1}", item.Path, jsonPath); // 使用 Emby ILogger 的 Info 方法
            return true;
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Error writing MediaInfo backup for {0} to {1}: {2}", ex, item.Path, jsonPath, ex.Message); // 使用 Emby ILogger 的 ErrorException 方法
            return false;
        }
    }

    public async Task<bool> RestoreMediaInfoAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var jsonPath = GetMediaInfoJsonPath(item);
        if (!_fileSystem.FileExists(jsonPath))
        {
            _logger.Debug("No MediaInfo backup file found for {0} at {1}", item.Path, jsonPath); // 使用 Emby ILogger 的 Debug 方法
            return false;
        }

        List<SerializableMediaSourceInfo>? mediaInfoToRestore;
        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            mediaInfoToRestore = JsonSerializer.Deserialize<List<SerializableMediaSourceInfo>>(jsonContent);
            if (mediaInfoToRestore == null || mediaInfoToRestore.Count == 0)
            {
                _logger.Warn("MediaInfo backup file {0} for {1} is empty or invalid.", jsonPath, item.Path); // 使用 Emby ILogger 的 Warn 方法
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Error reading or deserializing MediaInfo backup for {0} from {1}: {2}", ex, item.Path, jsonPath, ex.Message); // 使用 Emby ILogger 的 ErrorException 方法
            return false;
        }

        // Use the first MediaSourceInfo from the backup for restoration
        var sourceToRestore = mediaInfoToRestore[0]; // Assuming single source for simplicity, might need iteration

        // Restore MediaStreams
        var mediaStreamsToRestore = new List<MediaStream>();
        foreach (var stream in sourceToRestore.MediaStreams)
        {
            // Convert SerializableMediaStream back to MediaStream
            var restoredStream = new MediaStream
            {
                Type = stream.Type,
                Codec = stream.Codec,
                Language = stream.Language,
                Channels = stream.Channels,
                BitRate = stream.BitRate,
                BitDepth = stream.BitDepth,
                SampleRate = stream.SampleRate,
                Height = stream.Height,
                Width = stream.Width,
                AverageFrameRate = stream.AverageFrameRate,
                RealFrameRate = stream.RealFrameRate,
                Profile = stream.Profile,
                Level = stream.Level,
                IsDefault = stream.IsDefault,
                IsForced = stream.IsForced,
                IsInterlaced = stream.IsInterlaced,
                // IsAVC = stream.IsAVC, // REMOVED: Obsolete property
                Comment = stream.Comment,
                TimeBase = stream.TimeBase,
                CodecTag = stream.CodecTag,
                // Map other properties back
                Extradata = stream.Extradata,
                NalLengthSize = stream.NalLengthSize,
                PixelFormat = stream.PixelFormat,
                ColorSpace = stream.ColorSpace,
                ColorTransfer = stream.ColorTransfer,
                ColorPrimaries = stream.ColorPrimaries,
                // --- REMOVED: Properties not in MediaStream ---
                // DvVersionMajor = stream.DvVersionMajor,
                // DvVersionMinor = stream.DvVersionMinor,
                // DvProfile = stream.DvProfile,
                // DvLevel = stream.DvLevel,
                // RpuPresentFlag = stream.RpuPresentFlag,
                // ElPresentFlag = stream.ElPresentFlag,
                // BlPresentFlag = stream.BlPresentFlag,
                // DvBlSignalCompatibilityId = stream.DvBlSignalCompatibilityId,
                Title = stream.Title,
                // VideoRange = stream.VideoRange, // Read only, cannot assign
                // VideoRangeType = stream.VideoRangeType, // Not a property of MediaStream
                // VideoDoViTitle = stream.VideoDoViTitle, // Not a property of MediaStream
                RefFrames = stream.RefFrames,
                // PacketLength = stream.PacketLength, // Not a property of MediaStream
                ChannelLayout = stream.ChannelLayout,
                IsAnamorphic = stream.IsAnamorphic,
                AspectRatio = stream.AspectRatio,
                Index = stream.Index,
                // Score = stream.Score, // Not a property of MediaStream
                IsExternal = stream.IsExternal,
                DeliveryMethod = stream.DeliveryMethod,
                DeliveryUrl = stream.DeliveryUrl,
                IsExternalUrl = stream.IsExternalUrl,
                // Path should remain null or be set appropriately if external
                Path = null, // Usually null for restored streams, unless they are external files
                // Id, ItemId, ServerId should not be set during restore, they are internal
            };
            mediaStreamsToRestore.Add(restoredStream);
        }

        // Save MediaStreams to the database
        try
        {
            _itemRepository.SaveMediaStreams(item.InternalId, mediaStreamsToRestore, cancellationToken);
            _logger.Info("MediaStreams restored for {0} from {1}", item.Path, jsonPath); // 使用 Emby ILogger 的 Info 方法
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Error saving MediaStreams for {0} from {1}: {2}", ex, item.Path, jsonPath, ex.Message); // 使用 Emby ILogger 的 ErrorException 方法
            return false;
        }

        // Update BaseItem properties
        item.RunTimeTicks = sourceToRestore.RunTimeTicks;
        item.Size = sourceToRestore.Size ?? 0; // Handle long? to long conversion
        item.Container = sourceToRestore.Container;
        item.TotalBitrate = sourceToRestore.Bitrate ?? 0; // Fallback to 0 if null

        // Find the default video stream if possible
        var videoStream = mediaStreamsToRestore.FirstOrDefault(s => s.Type == MediaStreamType.Video && s.Width.HasValue && s.Height.HasValue);
        if (videoStream != null) // Check if videoStream is not null
        {
            item.Width = videoStream.Width ?? 0; // Handle nullable int? to int conversion
            item.Height = videoStream.Height ?? 0; // Handle nullable int? to int conversion
        }

        // Update the BaseItem in the library
        // CORRECTED: Check the actual signature of ILibraryManager.UpdateItems
        // Common signature might be: UpdateItems(List<BaseItem> items, BaseItem parent, ItemUpdateType updateType, ...)
        try
        {
            // Attempt with common signature: items, parent, updateType, metadataRefreshOptions, cancellationToken
            _libraryManager.UpdateItems(
                new List<BaseItem> { item },
                null,                        // parent
                ItemUpdateType.MetadataImport, // updateType
                null,                        // metadataRefreshOptions
                cancellationToken            // cancellationToken
            );
            _logger.Info("BaseItem metadata updated for {0} after MediaInfo restore.", item.Path); // 使用 Emby ILogger 的 Info 方法
            return true;
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Error updating BaseItem for {0} after MediaInfo restore: {1}", ex, item.Path, ex.Message); // 使用 Emby ILogger 的 ErrorException 方法
            return false;
        }
    }

    private string GetMediaInfoJsonPath(BaseItem item)
    {
        if (string.IsNullOrEmpty(item.Path))
        {
            throw new ArgumentException("Item path is null or empty.", nameof(item));
        }
        var directory = Path.GetDirectoryName(item.Path);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        return Path.Combine(directory ?? "", $"{fileNameWithoutExtension}.mediainfo");
    }
}

// DTOs remain the same
public class SerializableMediaSourceInfo
{
    public string? Container { get; set; }
    public long? RunTimeTicks { get; set; }
    public long? Size { get; set; } // Keep as long? for serialization
    public int? Bitrate { get; set; }
    public List<SerializableMediaStream> MediaStreams { get; set; } = new();
    // Add other properties you need to backup, excluding Emby internal IDs
    public string? Name { get; set; }
    public MediaProtocol Protocol { get; set; }
    public bool IsRemote { get; set; }
    public bool SupportsDirectPlay { get; set; }
    public bool SupportsDirectStream { get; set; }
    public bool SupportsTranscoding { get; set; }
    public long? ContainerStartTimeTicks { get; set; } // Add this property
}

public class SerializableMediaStream
{
    public MediaStreamType Type { get; set; }
    public string? Codec { get; set; }
    public string? Language { get; set; }
    public int? Channels { get; set; }
    public int? BitRate { get; set; }
    public int? BitDepth { get; set; }
    public int? SampleRate { get; set; }
    public int? Height { get; set; }
    public int? Width { get; set; }
    public float? AverageFrameRate { get; set; }
    public float? RealFrameRate { get; set; }
    public string? Profile { get; set; }
    public double? Level { get; set; }
    public bool IsDefault { get; set; }
    public bool IsForced { get; set; }
    public bool IsInterlaced { get; set; }
    // public bool? IsAVC { get; set; } // REMOVED: Obsolete property
    public string? Comment { get; set; }
    public string? TimeBase { get; set; }
    public string? CodecTag { get; set; }
    // Add other properties you need to backup, excluding Emby internal IDs and Path
    public string? Extradata { get; set; }
    public string? NalLengthSize { get; set; }
    public string? PixelFormat { get; set; }
    public string? ColorSpace { get; set; }
    public string? ColorTransfer { get; set; }
    public string? ColorPrimaries { get; set; }
    // --- REMOVED: Properties not in MediaStream ---
    // public int? DvVersionMajor { get; set; }
    // public int? DvVersionMinor { get; set; }
    // public int? DvProfile { get; set; }
    // public int? DvLevel { get; set; }
    // public int? RpuPresentFlag { get; set; }
    // public int? ElPresentFlag { get; set; }
    // public int? BlPresentFlag { get; set; }
    // public int? DvBlSignalCompatibilityId { get; set; }
    public string? Title { get; set; }
    public string? VideoRange { get; set; } // Keep for backup (read-only for original MediaStream)
    // public string? VideoRangeType { get; set; } // Not a property of MediaStream
    // public string? VideoDoViTitle { get; set; } // Not a property of MediaStream
    public int? RefFrames { get; set; }
    // public int? PacketLength { get; set; } // Not a property of MediaStream
    public string? ChannelLayout { get; set; }
    public bool? IsAnamorphic { get; set; }
    public string? AspectRatio { get; set; }
    public int Index { get; set; }
    // public int? Score { get; set; } // Not a property of MediaStream
    public bool IsExternal { get; set; }
    public SubtitleDeliveryMethod? DeliveryMethod { get; set; }
    public string? DeliveryUrl { get; set; }
    public bool? IsExternalUrl { get; set; }
    // NOTE: Do NOT include Id, ItemId, ServerId, Path
    public string? Id { get; set; } = null; // Explicitly set to null or omit
    public string? ItemId { get; set; } = null;
    public string? ServerId { get; set; } = null;
    public string? Path { get; set; } = null;
}
