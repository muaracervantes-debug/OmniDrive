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
    public string Icon => IsFolder ? "📁" : "▤";
    public string? FolderKey { get; }
    public bool IsFavorite { get; }
    public bool HasSavedHash { get; }

    public TelegramFileItem(string name, string chatName, string sizeLabel, string dateLabel, long sizeBytes, int messageId, InputPeer peer, bool isFolder = false, string? folderKey = null, IReadOnlyList<TelegramFilePart>? parts = null, string? splitUuid = null, int expectedPartCount = 1, bool isIncomplete = false, string? displayName = null, string? partsLabel = null, bool isFavorite = false, bool hasSavedHash = false)
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
        FolderKey = folderKey;
        IsFavorite = isFavorite;
        HasSavedHash = hasSavedHash;
        MessageDate = DateTimeOffset.TryParse(dateLabel, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }
}


public sealed record TelegramFilePart(
    string Name,
    InputPeer Peer,
    int MessageId,
    int PartNumber,
    int TotalParts,
    long SizeBytes);
