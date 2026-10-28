using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OmniDrive.App;

public sealed class TransferQueueItem : INotifyPropertyChanged
{
    private string _icon = "📄";
    private string _fileName = "";
    private double _percentage;
    private string _progressLabel = "Na fila";
    private string _partDescription = "—";
    private string _service = "—";
    private string _credential = "—";
    private string _speed = "Velocidade: —";
    private string _elapsed = "—";
    private string _remaining = "Restante: calculando...";
    private string _size = "—";
    private string _status = "Aguardando";
    private bool _cancelRequested;
    private bool _canRetry;
    public Func<Task>? RetryAction { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Icon { get => _icon; set => SetField(ref _icon, value); }
    public string FileName { get => _fileName; set => SetField(ref _fileName, value); }
    public double Percentage { get => _percentage; set => SetField(ref _percentage, value); }
    public string ProgressLabel { get => _progressLabel; set => SetField(ref _progressLabel, value); }
    public string PartDescription { get => _partDescription; set => SetField(ref _partDescription, value); }
    public string Service { get => _service; set => SetField(ref _service, value); }
    public string Credential { get => _credential; set => SetField(ref _credential, value); }
    public string Speed { get => _speed; set => SetField(ref _speed, value); }
    public string Elapsed { get => _elapsed; set => SetField(ref _elapsed, value); }
    public string Remaining { get => _remaining; set => SetField(ref _remaining, value); }
    public string Size { get => _size; set => SetField(ref _size, value); }
    public string Status { get => _status; set => SetField(ref _status, value); }
    public bool CancelRequested { get => _cancelRequested; set => SetField(ref _cancelRequested, value); }
    public bool CanRetry { get => _canRetry; set => SetField(ref _canRetry, value); }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}