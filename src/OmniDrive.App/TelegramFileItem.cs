using System;
using TL;
using System.Collections.Generic;

namespace OmniDrive.App;

public sealed class TelegramFileItem
{
    public string Name { get; }
    public string DisplayName { get; }
    public string PartsLabel { get; }
    public bool IsSplitFile { get; }
    public bool IsIncomplete { get; }
    public string? SplitUuid { get; }
    public int ExpectedPartCount { get; }
    public IReadOnlyList<TelegramFilePart> Parts { get; }
    public string ChatName { get; }
    public string SizeLabel { get; }
    public string DateLabel { get; }
    public long SizeBytes { get; }
    public int MessageId { get; }
    public InputPeer Peer { get; }
    public DateTimeOffset MessageDate { get; }
    public bool IsFolder { get; }
    public bool IsArchiveEntry { get; }
    public bool IsArchiveDirectory { get; }
    public string? ArchiveEntryKey { get; }
    public string Icon => GetFileIcon(DisplayName, IsFolder);

    private static string GetFileIcon(string fileName, bool isFolder)
    {
        if (isFolder)
            return "📁";

        var name = fileName.Trim().ToLowerInvariant();
        var extension = System.IO.Path.GetExtension(name);

        if (name.EndsWith(".tar.gz", StringComparison.Ordinal))
            return "🗜️";

        return extension switch
        {
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".tgz"
                or ".bz2" or ".xz" or ".lz" or ".lzma" or ".cab" or ".iso"
                => "🗜️",

            ".mp4" or ".m4v" or ".mkv" or ".mov" or ".avi" or ".wmv"
                or ".webm" or ".mpeg" or ".mpg" or ".3gp" or ".ts"
                => "🎬",

            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp"
                or ".tif" or ".tiff" or ".svg" or ".heic" or ".heif" or ".ico"
                => "🖼️",

            ".pdf" => "📕",

            ".txt" or ".md" or ".markdown" or ".rtf" or ".csv" or ".tsv"
                or ".log" or ".json" or ".xml" or ".yaml" or ".yml" or ".ini"
                or ".cfg" or ".conf" or ".html" or ".htm" or ".css" or ".js"
                or ".ts" or ".cs" or ".py" or ".java" or ".sql" or ".sh"
                => "📝",

            _ => "📦"
        };
    }
    public string? FolderKey { get; }
    public bool IsFavorite { get; set; }
    public bool HasSavedHash { get; }
    public string? SavedHash { get; }
    public string? ExpectedWholeFileHash { get; }

    public TelegramFileItem(string name, string chatName, string sizeLabel, string dateLabel, long sizeBytes, int messageId, InputPeer peer, bool isFolder = false, string? folderKey = null, IReadOnlyList<TelegramFilePart>? parts = null, string? splitUuid = null, int expectedPartCount = 1, bool isIncomplete = false, string? displayName = null, string? partsLabel = null, bool isFavorite = false, bool hasSavedHash = false, string? savedHash = null, string? expectedWholeFileHash = null, string? archiveEntryKey = null, bool isArchiveEntry = false, bool isArchiveDirectory = false)
    {
        Name = name;
        DisplayName = displayName ?? name;
        PartsLabel = partsLabel ?? string.Empty;
        IsSplitFile = splitUuid is not null;
        IsIncomplete = isIncomplete;
        SplitUuid = splitUuid;
        ExpectedPartCount = expectedPartCount;
        Parts = parts ?? Array.Empty<TelegramFilePart>();
        ChatName = chatName;
        SizeLabel = sizeLabel;
        DateLabel = dateLabel;
        SizeBytes = sizeBytes;
        MessageId = messageId;
        Peer = peer;
        IsFolder = isFolder;
        ArchiveEntryKey = archiveEntryKey;
        IsArchiveEntry = isArchiveEntry;
        IsArchiveDirectory = isArchiveDirectory;
        FolderKey = folderKey;
        IsFavorite = isFavorite;
        SavedHash = savedHash;
        ExpectedWholeFileHash = expectedWholeFileHash;
        HasSavedHash = hasSavedHash || !string.IsNullOrWhiteSpace(savedHash);
        MessageDate = DateTimeOffset.TryParse(dateLabel, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }
}


public sealed record TelegramFilePart(
    string Name,
    InputPeer Peer,
    int MessageId,
    int PartNumber,
    int TotalParts,
    long SizeBytes,
    string? SavedHash = null);
