namespace OmniDrive.App;

public sealed class TransferQueueItem
{
    public string Icon { get; set; } = "📄";
    public string FileName { get; set; } = "";
    public double Percentage { get; set; }
    public string ProgressLabel => $"{Percentage:0.##}%";
    public string PartDescription { get; set; } = "—";
    public string Service { get; set; } = "—";
    public string Credential { get; set; } = "—";
    public string Speed { get; set; } = "—";
    public string Elapsed { get; set; } = "—";
    public string Size { get; set; } = "—";
    public bool CancelRequested { get; set; }
}
