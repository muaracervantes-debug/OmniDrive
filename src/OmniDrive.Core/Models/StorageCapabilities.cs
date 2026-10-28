namespace OmniDrive.Core.Models;

/// <summary>Operations supported for a particular account or item.</summary>
[Flags]
public enum StorageCapabilities
{
    None = 0,
    List = 1 << 0,
    Search = 1 << 1,
    Upload = 1 << 2,
    Download = 1 << 3,
    CreateFolder = 1 << 4,
    Rename = 1 << 5,
    Delete = 1 << 6,
    Move = 1 << 7,
    Copy = 1 << 8
}
