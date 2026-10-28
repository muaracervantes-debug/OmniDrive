using OmniDrive.Core.Models;

namespace OmniDrive.Core.Tests;

public sealed class StorageCapabilitiesTests
{
    [Fact]
    public void Capabilities_CanBeCombinedAndChecked()
    {
        var capabilities = StorageCapabilities.List | StorageCapabilities.Download;

        Assert.True(capabilities.HasFlag(StorageCapabilities.List));
        Assert.True(capabilities.HasFlag(StorageCapabilities.Download));
        Assert.False(capabilities.HasFlag(StorageCapabilities.Upload));
    }

    [Fact]
    public void TransferProgress_ReportsPercentageWhenTotalIsKnown()
    {
        var progress = new TransferProgress(25, 100);

        Assert.Equal(25d, progress.Percentage);
    }

    [Fact]
    public void TransferProgress_LeavesPercentageUnknownWhenTotalIsMissing()
    {
        var progress = new TransferProgress(25);

        Assert.Null(progress.Percentage);
    }
}
