using System;
using TL;

namespace OmniDrive.App;

public sealed class TelegramFileItem
{
    public string Name { get; }
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

    public TelegramFileItem(string name, string chatName, string sizeLabel, string dateLabel, long sizeBytes, int messageId, InputPeer peer, bool isFolder = false, string? folderKey = null)
    {
        Name = name;
        ChatName = chatName;
        SizeLabel = sizeLabel;
        DateLabel = dateLabel;
        SizeBytes = sizeBytes;
        MessageId = messageId;
        Peer = peer;
        IsFolder = isFolder;
        FolderKey = folderKey;
        MessageDate = DateTimeOffset.TryParse(dateLabel, out var parsed) ? parsed : DateTimeOffset.MinValue;
    }
}
