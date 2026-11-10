namespace OmniDrive.Core.Models;

public sealed record TransferProgress(
    long BytesTransferred,
    long? TotalBytes = null,
    string? Status = null)
{
    public double? Percentage =>
        TotalBytes is > 0 ? Math.Clamp(BytesTransferred * 100d / TotalBytes.Value, 0d, 100d) : null;
}
