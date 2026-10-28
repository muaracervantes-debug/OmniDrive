using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Input;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using WTelegram;
using TL;

namespace OmniDrive.App;

public partial class MainWindow : Window
{
    private bool _detailsVisible = true;
    private string? _defaultDownloadFolder;
    private TelegramFileItem? _pendingDownloadFile;
    private bool _downloadDecryptMode;
    private readonly string _appPasswordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDrive", "app-password.json");
    private bool _appPasswordConfigured;
    private bool _appLocked;
    private string _appPasswordDialogMode = "manage";
    private readonly List<TelegramFolderLocation> _folderPath = new();
    private List<TelegramFileItem> _loadedCloudFiles = new();
    private readonly List<UploadEntry> _pendingUploads = new();
    private List<UploadDestination> _uploadDestinations = new() { new UploadDestination("Mensagens salvas", new InputPeerSelf(), "self") };
    private bool _uploadIsFolder;
    private readonly Dictionary<string, string?> _folderParents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _favoriteKeys = new(StringComparer.Ordinal);
    private readonly string _favoritesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDrive", "favorites.json");
    private const string TelegramFolderMarker = "_OD[Pasta]";
    private Client? _telegramClient;
    private bool _hasSavedTelegramCredentials;
    private bool _editingTelegramCredential;
    private string? _telegramPendingKind;
    private bool _telegramResendRequested;
    private readonly List<string> _connections = new();
    private readonly Dictionary<string, string> _uploadLimits = new();
    private int _completedTransfers;
    private TransferQueueItem? _selectedTransfer;
    public ObservableCollection<TransferQueueItem> TransferQueue { get; } = new();
    public ObservableCollection<TelegramFileItem> CloudFiles { get; } = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive",
        "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        TransferItemsList.ItemsSource = TransferQueue;
        CloudFilesList = this.FindControl<ListBox>(nameof(CloudFilesList)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CloudFilesList");
        CloudFilesList.ItemsSource = CloudFiles;
        CloudFilesList.SelectionChanged += CloudFilesList_SelectionChanged;
        CloudFilesContainer = this.FindControl<Grid>(nameof(CloudFilesContainer)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CloudFilesContainer");
        TelegramFilesStatus = this.FindControl<TextBlock>(nameof(TelegramFilesStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramFilesStatus");
        UpdateTransferQueueDisplay();
        UpdateTransferQueuePosition();
        LoadSettings();
        LoadTelegramFolderMap();
        LoadFavoriteKeys();
        LoadTelegramCredentials();
        _appPasswordConfigured = LoadAppPasswordRecord() is not null;
        RefreshAppPasswordSettings();
        Loaded += (_, _) =>
        {
            if (_appPasswordConfigured)
                ShowAppPasswordUnlock();
            else
                ContinueStartupAfterUnlock();
        };
        RefreshConnectionControls();
        UpdateIntegrityWarnings();
        Closed += (_, _) => _telegramClient?.Dispose();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

        // Explicitly bind named controls after loading the XAML. Without this,
        // generated fields can remain null and crash the window constructor.
        OverviewNav = this.FindControl<Button>(nameof(OverviewNav)) ?? throw new InvalidOperationException("Controle XAML não encontrado: OverviewNav");
        CloudDriveNav = this.FindControl<Button>(nameof(CloudDriveNav)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CloudDriveNav");
        TrashNav = this.FindControl<Button>(nameof(TrashNav)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TrashNav");
        SettingsNav = this.FindControl<Button>(nameof(SettingsNav)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsNav");
        DriveContent = this.FindControl<Grid>(nameof(DriveContent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DriveContent");
        AreaTitle = this.FindControl<TextBlock>(nameof(AreaTitle)) ?? throw new InvalidOperationException("Controle XAML não encontrado: AreaTitle");
        SearchBox = this.FindControl<TextBox>(nameof(SearchBox)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SearchBox");
        FilterCount = this.FindControl<TextBlock>(nameof(FilterCount)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FilterCount");
        FilterModalOverlay = this.FindControl<Grid>(nameof(FilterModalOverlay)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FilterModalOverlay");
        NameFilter = this.FindControl<TextBox>(nameof(NameFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: NameFilter");
        NameRuleMode = this.FindControl<ToggleSwitch>(nameof(NameRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: NameRuleMode");
        SizeMinFilter = this.FindControl<TextBox>(nameof(SizeMinFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeMinFilter");
        SizeMaxFilter = this.FindControl<TextBox>(nameof(SizeMaxFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeMaxFilter");
        SizeRuleMode = this.FindControl<ToggleSwitch>(nameof(SizeRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeRuleMode");
        DateFromFilter = this.FindControl<DatePicker>(nameof(DateFromFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateFromFilter");
        DateToFilter = this.FindControl<DatePicker>(nameof(DateToFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateToFilter");
        DateRuleMode = this.FindControl<ToggleSwitch>(nameof(DateRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateRuleMode");
        ExtensionFilter = this.FindControl<TextBox>(nameof(ExtensionFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ExtensionFilter");
        ExtensionRuleMode = this.FindControl<ToggleSwitch>(nameof(ExtensionRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ExtensionRuleMode");
        ServiceFilter = this.FindControl<ComboBox>(nameof(ServiceFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ServiceFilter");
        ServiceRuleMode = this.FindControl<ToggleSwitch>(nameof(ServiceRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ServiceRuleMode");
        CredentialFilter = this.FindControl<ComboBox>(nameof(CredentialFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CredentialFilter");
        CredentialRuleMode = this.FindControl<ToggleSwitch>(nameof(CredentialRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CredentialRuleMode");
        FavoritesFilter = this.FindControl<ComboBox>(nameof(FavoritesFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FavoritesFilter");
        FavoritesRuleMode = this.FindControl<ToggleSwitch>(nameof(FavoritesRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FavoritesRuleMode");
        TypeFilter = this.FindControl<ComboBox>(nameof(TypeFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TypeFilter");
        TypeRuleMode = this.FindControl<ToggleSwitch>(nameof(TypeRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TypeRuleMode");
        HashFilter = this.FindControl<ComboBox>(nameof(HashFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: HashFilter");
        HashRuleMode = this.FindControl<ToggleSwitch>(nameof(HashRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: HashRuleMode");
        PartsCountFilter = this.FindControl<TextBox>(nameof(PartsCountFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: PartsCountFilter");
        PartsRuleMode = this.FindControl<ToggleSwitch>(nameof(PartsRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: PartsRuleMode");
        BreadcrumbPrevious = this.FindControl<Button>(nameof(BreadcrumbPrevious)) ?? throw new InvalidOperationException("Controle XAML não encontrado: BreadcrumbPrevious");
        BreadcrumbSeparator = this.FindControl<TextBlock>(nameof(BreadcrumbSeparator)) ?? throw new InvalidOperationException("Controle XAML não encontrado: BreadcrumbSeparator");
        BreadcrumbCurrent = this.FindControl<Button>(nameof(BreadcrumbCurrent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: BreadcrumbCurrent");
        SelectionCount = this.FindControl<TextBlock>(nameof(SelectionCount)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SelectionCount");
        SelectAllCheckBox = this.FindControl<CheckBox>(nameof(SelectAllCheckBox)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SelectAllCheckBox");
        SelectAllCheckBox.Checked += SelectAllCheckBox_Changed;
        SelectAllCheckBox.Unchecked += SelectAllCheckBox_Changed;
        EmptyFilesState = this.FindControl<Grid>(nameof(EmptyFilesState)) ?? throw new InvalidOperationException("Controle XAML não encontrado: EmptyFilesState");
        FilesEmptyTitle = this.FindControl<TextBlock>(nameof(FilesEmptyTitle)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FilesEmptyTitle");
        FilesEmptyDescription = this.FindControl<TextBlock>(nameof(FilesEmptyDescription)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FilesEmptyDescription");
        DetailsPanel = this.FindControl<Border>(nameof(DetailsPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DetailsPanel");
        ShowDetailsButton = this.FindControl<Button>(nameof(ShowDetailsButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ShowDetailsButton");
        TransferQueueHost = this.FindControl<StackPanel>(nameof(TransferQueueHost)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferQueueHost");
        SettingsContent = this.FindControl<Grid>(nameof(SettingsContent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsContent");
        SettingsConnectPage = this.FindControl<ScrollViewer>(nameof(SettingsConnectPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsConnectPage");
        SettingsTransfersPage = this.FindControl<ScrollViewer>(nameof(SettingsTransfersPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsTransfersPage");
        SettingsSecurityPage = this.FindControl<ScrollViewer>(nameof(SettingsSecurityPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsSecurityPage");
        _ = this.FindControl<TextBlock>(nameof(AppPasswordSettingsStatus));
        _ = this.FindControl<Button>(nameof(ManageAppPasswordButton));
        _ = this.FindControl<Grid>(nameof(AppPasswordOverlay));
        _ = this.FindControl<TextBlock>(nameof(AppPasswordDialogTitle));
        _ = this.FindControl<TextBlock>(nameof(AppPasswordDialogDescription));
        _ = this.FindControl<StackPanel>(nameof(AppPasswordCurrentPanel));
        _ = this.FindControl<TextBox>(nameof(AppPasswordCurrent));
        _ = this.FindControl<StackPanel>(nameof(AppPasswordNewPanel));
        _ = this.FindControl<TextBox>(nameof(AppPasswordNew));
        _ = this.FindControl<TextBox>(nameof(AppPasswordConfirm));
        _ = this.FindControl<TextBlock>(nameof(AppPasswordDialogStatus));
        _ = this.FindControl<Button>(nameof(RemoveAppPasswordButton));
        _ = this.FindControl<Button>(nameof(CancelAppPasswordButton));
        _ = this.FindControl<Button>(nameof(SaveAppPasswordButton));
                NewConnectionService = this.FindControl<ComboBox>(nameof(NewConnectionService)) ?? throw new InvalidOperationException("Controle XAML não encontrado: NewConnectionService");
        NewConnectionName = this.FindControl<TextBox>(nameof(NewConnectionName)) ?? throw new InvalidOperationException("Controle XAML não encontrado: NewConnectionName");
        ConnectionsList = this.FindControl<ListBox>(nameof(ConnectionsList)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ConnectionsList");
        UploadLimitConnection = this.FindControl<ComboBox>(nameof(UploadLimitConnection)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadLimitConnection");
        UploadLimitValue = this.FindControl<TextBox>(nameof(UploadLimitValue)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadLimitValue");
        MaxUploadsValue = this.FindControl<TextBox>(nameof(MaxUploadsValue)) ?? throw new InvalidOperationException("Controle XAML não encontrado: MaxUploadsValue");
        MaxDownloadsValue = this.FindControl<TextBox>(nameof(MaxDownloadsValue)) ?? throw new InvalidOperationException("Controle XAML não encontrado: MaxDownloadsValue");
        VerifyUploadHash = this.FindControl<CheckBox>(nameof(VerifyUploadHash)) ?? throw new InvalidOperationException("Controle XAML não encontrado: VerifyUploadHash");
        UploadIntegrityWarning = this.FindControl<TextBlock>(nameof(UploadIntegrityWarning)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadIntegrityWarning");
        VerifyDownloadHash = this.FindControl<CheckBox>(nameof(VerifyDownloadHash)) ?? throw new InvalidOperationException("Controle XAML não encontrado: VerifyDownloadHash");
        DownloadIntegrityWarning = this.FindControl<TextBlock>(nameof(DownloadIntegrityWarning)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadIntegrityWarning");
        DefaultDownloadFolder = this.FindControl<TextBox>(nameof(DefaultDownloadFolder)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DefaultDownloadFolder");
        DownloadModalOverlay = this.FindControl<Grid>(nameof(DownloadModalOverlay)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadModalOverlay");
        DownloadModalCard = this.FindControl<Border>(nameof(DownloadModalCard)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadModalCard");
        DownloadModalSubtitle = this.FindControl<TextBlock>(nameof(DownloadModalSubtitle)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadModalSubtitle");
        DownloadFileName = this.FindControl<TextBox>(nameof(DownloadFileName)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadFileName");
        DownloadFolderPath = this.FindControl<TextBox>(nameof(DownloadFolderPath)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadFolderPath");
        DownloadVerifyHashCheckBox = this.FindControl<CheckBox>(nameof(DownloadVerifyHashCheckBox)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadVerifyHashCheckBox");
        DownloadEncryptCheckBox = this.FindControl<CheckBox>(nameof(DownloadEncryptCheckBox)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadEncryptCheckBox");
        DownloadEncryptionFields = this.FindControl<StackPanel>(nameof(DownloadEncryptionFields)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadEncryptionFields");
        DownloadEncryptionPassword = this.FindControl<TextBox>(nameof(DownloadEncryptionPassword)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadEncryptionPassword");
        DownloadEncryptionPasswordConfirm = this.FindControl<TextBox>(nameof(DownloadEncryptionPasswordConfirm)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadEncryptionPasswordConfirm");
        DownloadEncryptCheckBox.IsCheckedChanged += (_, _) => DownloadEncryptionFields.IsVisible = DownloadEncryptCheckBox.IsChecked == true;
        DownloadModalStatus = this.FindControl<TextBlock>(nameof(DownloadModalStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DownloadModalStatus");
        StartDownloadButton = this.FindControl<Button>(nameof(StartDownloadButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: StartDownloadButton");
        SettingsStatus = this.FindControl<TextBlock>(nameof(SettingsStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsStatus");
        StatusText = this.FindControl<TextBlock>(nameof(StatusText)) ?? throw new InvalidOperationException("Controle XAML não encontrado: StatusText");
        TransferQueuePanel = this.FindControl<Border>(nameof(TransferQueuePanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferQueuePanel");
        TransferItemsList = this.FindControl<ListBox>(nameof(TransferItemsList)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferItemsList");
        TransferQueueEmpty = this.FindControl<TextBlock>(nameof(TransferQueueEmpty)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferQueueEmpty");
        TransferQueueCount = this.FindControl<TextBlock>(nameof(TransferQueueCount)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferQueueCount");
        TransferDetailsOverlay = this.FindControl<Grid>(nameof(TransferDetailsOverlay)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailsOverlay");
        TransferDetailService = this.FindControl<TextBlock>(nameof(TransferDetailService)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailService");
        TransferDetailName = this.FindControl<TextBlock>(nameof(TransferDetailName)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailName");
        TransferDetailPercent = this.FindControl<TextBlock>(nameof(TransferDetailPercent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailPercent");
        TransferDetailSpeed = this.FindControl<TextBlock>(nameof(TransferDetailSpeed)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailSpeed");
        TransferDetailProgress = this.FindControl<ProgressBar>(nameof(TransferDetailProgress)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailProgress");
        TransferDetailElapsed = this.FindControl<TextBlock>(nameof(TransferDetailElapsed)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailElapsed");
        TransferDetailSize = this.FindControl<TextBlock>(nameof(TransferDetailSize)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailSize");
        TransferDetailCredential = this.FindControl<TextBlock>(nameof(TransferDetailCredential)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailCredential");
        TransferDetailPart = this.FindControl<TextBlock>(nameof(TransferDetailPart)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TransferDetailPart");
        TelegramSetupPanel = this.FindControl<Border>(nameof(TelegramSetupPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramSetupPanel");
        TelegramConnectionSummary = this.FindControl<TextBlock>(nameof(TelegramConnectionSummary)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramConnectionSummary");
        TelegramCardConnectButton = this.FindControl<Button>(nameof(TelegramCardConnectButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramCardConnectButton");
        TelegramSetupOverlay = this.FindControl<Grid>(nameof(TelegramSetupOverlay)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramSetupOverlay");
        TelegramConnectionName = this.FindControl<TextBox>(nameof(TelegramConnectionName)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramConnectionName");
        TelegramApiId = this.FindControl<TextBox>(nameof(TelegramApiId)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramApiId");
        TelegramApiHash = this.FindControl<TextBox>(nameof(TelegramApiHash)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramApiHash");
        TelegramPhone = this.FindControl<TextBox>(nameof(TelegramPhone)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramPhone");
        TelegramLoginInputPanel = this.FindControl<StackPanel>(nameof(TelegramLoginInputPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginInputPanel");
        TelegramLoginLabel = this.FindControl<TextBlock>(nameof(TelegramLoginLabel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginLabel");
        TelegramLoginInput = this.FindControl<TextBox>(nameof(TelegramLoginInput)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginInput");
        TelegramConnectButton = this.FindControl<Button>(nameof(TelegramConnectButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramConnectButton");
        TelegramStatus = this.FindControl<TextBlock>(nameof(TelegramStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramStatus");
        UploadModalOverlay = this.FindControl<Grid>(nameof(UploadModalOverlay)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadModalOverlay");
        UploadModalTitle = this.FindControl<TextBlock>(nameof(UploadModalTitle)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadModalTitle");
        UploadItemsPanel = this.FindControl<StackPanel>(nameof(UploadItemsPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadItemsPanel");
        UploadModalStatus = this.FindControl<TextBlock>(nameof(UploadModalStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: UploadModalStatus");
    }


    private async void CloudFilesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await RefreshSelectedItemDetailsAsync();
    }

    private async Task RefreshSelectedItemDetailsAsync()
    {
        var item = CloudFilesList.SelectedItems.OfType<TelegramFileItem>().LastOrDefault()
            ?? CloudFilesList.SelectedItem as TelegramFileItem;
        var unknown = "Informação não disponivel";
        var sizeText = item is null ? unknown : !item.IsFolder && item.SizeBytes > 0
            ? FormatFileSize(item.SizeBytes)
            : item.IsFolder ? "Calculando..." : (!string.IsNullOrWhiteSpace(item.SizeLabel) ? item.SizeLabel : unknown);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DetailName"] = item?.DisplayName ?? unknown,
            ["DetailSize"] = sizeText,
            ["DetailExtension"] = item is null ? unknown : item.IsFolder ? unknown : (Path.GetExtension(item.DisplayName) is { Length: > 0 } extension ? extension : "Sem extensão"),
            ["DetailCreated"] = unknown,
            ["DetailModified"] = item is not null && item.MessageDate != DateTimeOffset.MinValue
                ? item.MessageDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : unknown,
            ["DetailService"] = item is null ? unknown : "Telegram",
            ["DetailCredential"] = item is null ? unknown : _telegramClient?.User is not null ? (_telegramClient.User.username ?? _telegramClient.User.first_name ?? "Telegram conectado") : unknown,
            ["DetailFolder"] = item is null ? unknown : (_folderPath.LastOrDefault()?.Name ?? "Raiz do Drive"),
            ["DetailParts"] = item is null ? unknown : item.IsFolder ? "Não aplicável" : item.IsSplitFile
                ? $"{item.Parts.Count} parte(s) encontrada(s) de {item.ExpectedPartCount}" + (item.IsIncomplete ? " — incompleto" : "")
                : "1 parte",
            ["DetailSha256"] = item is null ? unknown : item.HasSavedHash && !string.IsNullOrWhiteSpace(item.SavedHash)
                ? item.SavedHash : unknown
        };

        foreach (var pair in values)
            if (this.FindControl<TextBlock>(pair.Key) is { } valueBlock)
                valueBlock.Text = pair.Value;

        if (item is not null && item.IsFolder)
        {
            try
            {
                var bytes = await CalculateFolderSizeAsync(item.FolderKey);
                if (CloudFilesList.SelectedItem == item || CloudFilesList.SelectedItems.Contains(item))
                    if (this.FindControl<TextBlock>("DetailSize") is { } sizeBlock)
                        sizeBlock.Text = FormatFileSize(bytes);
            }
            catch
            {
                if (this.FindControl<TextBlock>("DetailSize") is { } sizeBlock)
                    sizeBlock.Text = unknown;
            }
        }

        if (this.FindControl<TextBlock>("DetailSelectionTitle") is { } title)
            title.Text = item?.DisplayName ?? "Nenhum item selecionado";
        if (this.FindControl<TextBlock>("DetailSelectionDescription") is { } description)
            description.Text = item is null ? "Selecione um arquivo ou pasta para ver os detalhes."
                : item.IsFolder ? "Pasta selecionada." : "Arquivo selecionado.";
    }

    private async Task<long> CalculateFolderSizeAsync(string? rootFolderKey)
    {
        if (_telegramClient is null || string.IsNullOrWhiteSpace(rootFolderKey))
            return 0;

        var dialogs = await _telegramClient.Messages_GetAllDialogs();
        var folderPeers = new Dictionary<string, InputPeer>(StringComparer.Ordinal);
        foreach (var dialog in dialogs.dialogs)
        {
            var entity = dialogs.UserOrChat(dialog);
            if (entity is not (TL.Chat or Channel))
                continue;
            var chatName = entity switch
            {
                TL.Chat chat => chat.title,
                Channel channel => channel.title,
                _ => string.Empty
            };
            if (!chatName.EndsWith(TelegramFolderMarker, StringComparison.Ordinal))
                continue;
            var key = GetTelegramPeerKey(entity);
            InputPeer? peer = entity switch
            {
                TL.Chat chat => new InputPeerChat(chat.id),
                Channel channel => new InputPeerChannel(channel.id, channel.access_hash),
                _ => null
            };
            if (key is not null && peer is not null)
                folderPeers[key] = peer;
        }

        var total = 0L;
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(rootFolderKey);
        while (pending.Count > 0)
        {
            var folderKey = pending.Pop();
            if (!visited.Add(folderKey) || !folderPeers.TryGetValue(folderKey, out var peer))
                continue;

            var history = await _telegramClient.Messages_GetHistory(peer, limit: 100);
            foreach (var message in history.Messages.OfType<Message>())
                if (message.media is MessageMediaDocument mediaDocument && mediaDocument.document is Document document)
                    total += document.size;

            foreach (var child in folderPeers.Keys)
                if (_folderParents.TryGetValue(child, out var parent) && string.Equals(parent, folderKey, StringComparison.Ordinal))
                    pending.Push(child);
        }
        return total;
    }

    private void Overview_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Visão geral";
        StatusText.Text = "Nenhum serviço conectado";
        SetBreadcrumbRoot("Visão geral");
        SetSelectedNavigation(sender);
    }

    private void CloudDrive_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Drive na nuvem";
        StatusText.Text = "Aguardando conexão com um serviço";
        SetBreadcrumbRoot("Drive na nuvem");
        SetSelectedNavigation(sender);
    }

    private void Trash_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Lixeira";
        SetBreadcrumbRoot("Lixeira");

        // A integração atual é com o Telegram, que não oferece uma lixeira
        // consultável pelo OmniDrive. Não exiba os arquivos normais do drive
        // como se fossem itens excluídos.
        CloudFilesContainer.IsVisible = false;
        EmptyFilesState.IsVisible = true;
        FilesEmptyTitle.Text = "Nenhum provedor com lixeira disponível";
        FilesEmptyDescription.Text = "A lixeira reunirá os itens excluídos de cada credencial conectada quando o serviço oferecer esse recurso. A conexão atual do Telegram não disponibiliza uma lixeira recuperável.";
        TelegramFilesStatus.Text = string.Empty;
        StatusText.Text = "Lixeira indisponível para as credenciais conectadas";
        SetSelectedNavigation(sender);
    }

    private void SettingsSection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string section)
            return;

        SettingsConnectPage.IsVisible = section == "connect";
        SettingsTransfersPage.IsVisible = section == "transfers";
        SettingsSecurityPage.IsVisible = section == "security";

        foreach (var item in this.GetVisualDescendants().OfType<Button>())
        {
            if (item.Tag is string tag && new[] { "connect", "transfers", "security" }.Contains(tag))
            {
                item.Classes.Set("selected", tag == section);
            }
        }
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        DriveContent.IsVisible = false;
        DetailsPanel.IsVisible = false;
        ShowDetailsButton.IsVisible = false;
        SettingsContent.IsVisible = true;
        // A fila é um controle flutuante global e deve continuar acessível em Configurações.
        TransferQueueHost.IsVisible = true;
        UpdateTransferQueuePosition();
        SettingsStatus.Text = "Configurações locais. Conexões ainda não autenticam com os provedores.";
        SetSelectedNavigation(sender);
    }

    private void ShowDriveView()
    {
        SettingsContent.IsVisible = false;
        DriveContent.IsVisible = true;
        DetailsPanel.IsVisible = _detailsVisible;
        ShowDetailsButton.IsVisible = !_detailsVisible;
        UpdateTransferQueuePosition();
    }

    private void SetSelectedNavigation(object? sender)
    {
        if (sender is not Button selected)
            return;

        OverviewNav.Classes.Remove("selected");
        CloudDriveNav.Classes.Remove("selected");
        TrashNav.Classes.Remove("selected");
        SettingsNav.Classes.Remove("selected");
        selected.Classes.Add("selected");
    }

    private async void LoadTelegramFiles_Click(object? sender, RoutedEventArgs e) => await LoadTelegramFilesAsync();

    private async Task LoadTelegramFilesAsync()
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte o Telegram em Configurações → Conectar serviços antes de carregar arquivos.";
            EmptyFilesState.IsVisible = true;
            CloudFilesContainer.IsVisible = false;
            return;
        }

        TelegramFilesStatus.Text = "Detectando pastas do OmniDrive e consultando documentos do Telegram...";
        FilesEmptyTitle.Text = "Carregando arquivos…";
        FilesEmptyDescription.Text = "Estamos verificando os chats e as pastas identificadas pelo OmniDrive.";
        EmptyFilesState.IsVisible = true;
        CloudFilesContainer.IsVisible = false;
        CloudFiles.Clear();

        try
        {
            var dialogs = await _telegramClient.Messages_GetAllDialogs();
            var dialogList = dialogs.dialogs.ToList();
            var currentFolder = _folderPath.LastOrDefault();
            var scanned = 0;
            var foldersFound = 0;

            // Consultar diretamente o chat da pasta aberta. Não depender de ele aparecer
            // novamente na lista de diálogos do Telegram para conseguir ler seus arquivos.
            if (currentFolder is not null)
            {
                var folderHistory = await _telegramClient.Messages_GetHistory(currentFolder.Peer, limit: 100);
                foreach (var message in folderHistory.Messages.OfType<Message>())
                {
                    if (message.media is not MessageMediaDocument mediaDocument || mediaDocument.document is not Document document)
                        continue;

                    var fileName = document.attributes?
                        .OfType<DocumentAttributeFilename>()
                        .Select(attribute => attribute.file_name)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                    if (string.IsNullOrWhiteSpace(fileName))
                        fileName = $"Telegram_documento_{message.ID}";

                    var captionData = ParseTelegramCaption(message.message, fileName);
                    CloudFiles.Add(new TelegramFileItem(
                        captionData.Name,
                        currentFolder.Name,
                        FormatFileSize(document.size),
                        message.date.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                        document.size,
                        message.ID,
                        currentFolder.Peer,
                        hasSavedHash: captionData.Hash is not null,
                        savedHash: captionData.Hash));
                }
            }

            foreach (var dialog in dialogList)
            {
                try
                {
                    var entity = dialogs.UserOrChat(dialog);
                    if (entity is null)
                        continue;

                    var chatName = entity switch
                    {
                        TL.Chat chat => chat.title,
                        Channel channel => channel.title,
                        User user => user.ToString(),
                        _ => entity.ToString() ?? "Conversa do Telegram"
                    };
                    InputPeer? inputPeer = entity switch
                    {
                        User user => new InputPeerUser(user.id, user.access_hash),
                        TL.Chat chat => new InputPeerChat(chat.id),
                        Channel channel => new InputPeerChannel(channel.id, channel.access_hash),
                        _ => null
                    };
                    var peerKey = GetTelegramPeerKey(entity);
                    if (inputPeer is null || peerKey is null)
                        continue;

                    var isFolder = entity is TL.Chat or Channel
                        && chatName.EndsWith(TelegramFolderMarker, StringComparison.Ordinal);
                    if (isFolder)
                    {
                        foldersFound++;
                        var folderName = chatName[..^TelegramFolderMarker.Length];
                        _folderParents.TryGetValue(peerKey, out var parentKey);
                        if (string.Equals(parentKey, currentFolder?.Key, StringComparison.Ordinal))
                        {
                            CloudFiles.Add(new TelegramFileItem(
                                folderName,
                                "Pasta do Telegram · " + chatName,
                                "—",
                                "—",
                                0,
                                0,
                                inputPeer,
                                isFolder: true,
                                folderKey: peerKey));
                        }
                    }

                    // Ao navegar dentro de uma pasta, os arquivos já foram carregados
                    // diretamente do chat atual acima. Aqui só descobrimos subpastas.
                    if (currentFolder is not null)
                    {
                        scanned++;
                        continue;
                    }

                    if (isFolder)
                        continue;

                    var history = await _telegramClient.Messages_GetHistory(inputPeer, limit: 100);
                    foreach (var message in history.Messages.OfType<Message>())
                    {
                        if (message.media is not MessageMediaDocument mediaDocument || mediaDocument.document is not Document document)
                            continue;

                        var fileName = document.attributes?
                            .OfType<DocumentAttributeFilename>()
                            .Select(attribute => attribute.file_name)
                            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
                        if (string.IsNullOrWhiteSpace(fileName))
                            fileName = $"Telegram_documento_{message.ID}";

                        var captionData = ParseTelegramCaption(message.message, fileName);
                        var date = message.date.ToLocalTime();
                        CloudFiles.Add(new TelegramFileItem(
                            captionData.Name,
                            chatName,
                            FormatFileSize(document.size),
                            date.ToString("dd/MM/yyyy HH:mm"),
                            document.size,
                            message.ID,
                            inputPeer,
                            hasSavedHash: captionData.Hash is not null,
                            savedHash: captionData.Hash));
                    }
                    scanned++;
                }
                catch
                {
                    // Um diálogo inacessível não deve impedir a detecção dos demais chats e pastas.
                }
            }

            foreach (var item in CloudFiles)
                item.IsFavorite = _favoriteKeys.Contains(GetFavoriteKey(item));

            var sorted = GroupSplitFiles(CloudFiles.ToList())
                .OrderBy(file => file.IsFolder ? 0 : 1)
                .ThenByDescending(file => file.MessageDate)
                .ToList();
            _loadedCloudFiles = sorted;
            ApplyCurrentFilters();

            if (_loadedCloudFiles.Count == 0)
            {
                FilesEmptyTitle.Text = currentFolder is null ? "Nenhum arquivo ou pasta encontrado" : "Esta pasta está vazia";
                FilesEmptyDescription.Text = currentFolder is null
                    ? $"Foram identificadas {foldersFound} pasta(s) do OmniDrive. Para criar uma, use Novo → Criar pasta."
                    : "Não há documentos ou subpastas nesta pasta. Você pode criar uma subpasta pelo menu Novo.";
                EmptyFilesState.IsVisible = true;
                CloudFilesContainer.IsVisible = false;
                TelegramFilesStatus.Text = currentFolder is null
                    ? $"Nenhum item encontrado. Pastas do OmniDrive detectadas: {foldersFound}."
                    : $"Pasta atual: {currentFolder.Name}. Nenhum item encontrado.";
            }
            else if (GetActiveFilterCount() == 0)
            {
                // A listagem de arquivos não precisa exibir o resumo de quantidade/pastas.
                // Mantemos o espaço de status disponível para mensagens de erro e filtros.
                TelegramFilesStatus.Text = string.Empty;
            }
            UpdateBreadcrumb();
        }
        catch (Exception ex)
        {
            var explanation = ExplainTransferError(ex, "consultar os arquivos", "Telegram");
            FilesEmptyTitle.Text = "Não foi possível carregar arquivos e pastas";
            FilesEmptyDescription.Text = explanation;
            EmptyFilesState.IsVisible = true;
            CloudFilesContainer.IsVisible = false;
            TelegramFilesStatus.Text = explanation;
            if (explanation.Contains("autenticação", StringComparison.OrdinalIgnoreCase))
                MarkTelegramDisconnected(explanation);
        }
    }

    private static readonly Regex TelegramHashMarkerRegex = new(
        @"SHA-256_OD\[(?<hash>[0-9a-fA-F]{64})\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyTelegramHashMarkerRegex = new(
        @"_OG\[(?<hash>[0-9a-fA-F]{64})\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static (string Name, string? Hash) ParseTelegramCaption(string? caption, string fallbackName)
    {
        var filenameMatch = LegacyTelegramHashMarkerRegex.Match(fallbackName);
        var filenameHash = filenameMatch.Success ? filenameMatch.Groups["hash"].Value.ToLowerInvariant() : null;
        var cleanFallbackName = LegacyTelegramHashMarkerRegex.Replace(fallbackName, string.Empty);

        if (string.IsNullOrWhiteSpace(caption))
            return (cleanFallbackName, filenameHash);

        var match = TelegramHashMarkerRegex.Match(caption);
        if (!match.Success)
            match = LegacyTelegramHashMarkerRegex.Match(caption);
        if (!match.Success)
            return (caption.Trim(), filenameHash);

        var name = caption[..match.Index].TrimEnd();
        if (string.IsNullOrWhiteSpace(name))
            name = cleanFallbackName;
        return (name, match.Groups["hash"].Value.ToLowerInvariant());
    }

    private static string AddHashMarkerToFileName(string fileName, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || TelegramHashMarkerRegex.IsMatch(fileName))
            return fileName;

        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = extension.Length > 0 ? fileName[..^extension.Length] : fileName;
        return $"{nameWithoutExtension}_OG[{hash}]{extension}";
    }

    private static string BuildTelegramCaption(string fileName, string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? fileName : $"{fileName} SHA-256_OD[{hash}]";

    private static string RenameTelegramCaption(string? existingCaption, string fallbackName, string newName)
    {
        if (string.IsNullOrWhiteSpace(existingCaption))
            return newName;

        var metadata = Regex.Match(
            existingCaption,
            @"\s+(?:SHA-256_OD|_OG)\[.*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return metadata.Success ? newName + metadata.Value : newName;
    }

    private static List<TelegramFileItem> GroupSplitFiles(IReadOnlyList<TelegramFileItem> files)
    {
        var result = new List<TelegramFileItem>();
        var groups = new Dictionary<string, List<(TelegramFileItem File, SplitFileName Parsed)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file.IsFolder || !TryParseSplitFileName(file.Name, out var parsed))
            {
                result.Add(file);
                continue;
            }

            // The UUID embedded in _OD[...] is the stable identity of a split file.
            // SHA-256 is integrity metadata only and must never be used to group parts.
            var groupKey = "uuid:" + parsed.Uuid;

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new List<(TelegramFileItem File, SplitFileName Parsed)>();
                groups[groupKey] = group;
            }
            group.Add((file, parsed));
        }

        foreach (var pair in groups)
        {
            var entries = pair.Value
                .GroupBy(entry => entry.Parsed.PartNumber)
                .Select(group => group.OrderByDescending(entry => entry.File.MessageDate).First())
                .OrderBy(entry => entry.Parsed.PartNumber)
                .ToList();
            if (entries.Count == 0)
                continue;

            var expectedTotal = entries.Max(entry => entry.Parsed.TotalParts);
            var actualNumbers = entries.Select(entry => entry.Parsed.PartNumber).ToHashSet();
            var incomplete = entries.Any(entry => entry.Parsed.TotalParts != expectedTotal)
                || actualNumbers.Count != expectedTotal
                || Enumerable.Range(1, expectedTotal).Any(partNumber => !actualNumbers.Contains(partNumber));
            var first = entries[0];
            var displayName = first.Parsed.OriginalName;
            var totalBytes = entries.Sum(entry => entry.File.SizeBytes);
            var parts = entries.Select(entry => new TelegramFilePart(
                entry.File.Name,
                entry.File.Peer,
                entry.File.MessageId,
                entry.Parsed.PartNumber,
                entry.Parsed.TotalParts,
                entry.File.SizeBytes,
                entry.File.SavedHash)).ToList();
            var partsLabel = incomplete
                ? $"{entries.Count}/{expectedTotal} partes · incompleto"
                : $"{expectedTotal} partes";
            var sourceChats = entries.Select(entry => entry.File.ChatName).Distinct(StringComparer.Ordinal).ToList();
            var chatLabel = sourceChats.Count == 1 ? sourceChats[0] : $"Telegram · {sourceChats.Count} chats";
            var savedHash = entries.Select(entry => entry.File.SavedHash)
                .FirstOrDefault(hash => !string.IsNullOrWhiteSpace(hash));

            result.Add(new TelegramFileItem(
                displayName,
                chatLabel,
                FormatFileSize(totalBytes),
                entries.Max(entry => entry.File.MessageDate).ToString("dd/MM/yyyy HH:mm"),
                totalBytes,
                first.File.MessageId,
                first.File.Peer,
                parts: parts,
                splitUuid: first.Parsed.Uuid,
                expectedPartCount: expectedTotal,
                isIncomplete: incomplete,
                displayName: displayName,
                partsLabel: partsLabel,
                hasSavedHash: savedHash is not null,
                savedHash: savedHash));
        }

        return result;
    }

    private static bool TryParseSplitFileName(string name, out SplitFileName parsed)
    {
        parsed = default!;
        var match = Regex.Match(
            name,
            @"^(?<base>.+?)_OD\[(?<uuid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})_(?<part>[1-9][0-9]*)-(?<total>[1-9][0-9]*)\](?:_OG\[(?<hash>[0-9a-fA-F]{64})\])?(?<ext>\..*)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !Guid.TryParse(match.Groups["uuid"].Value, out var uuid)
            || !int.TryParse(match.Groups["part"].Value, out var partNumber)
            || !int.TryParse(match.Groups["total"].Value, out var totalParts)
            || partNumber > totalParts)
            return false;

        var originalName = match.Groups["base"].Value + match.Groups["ext"].Value;
        parsed = new SplitFileName(originalName, uuid.ToString(), partNumber, totalParts);
        return true;
    }

    private sealed record SplitFileName(string OriginalName, string Uuid, int PartNumber, int TotalParts);

    private static string? GetTelegramPeerKey(object entity) => entity switch
    {
        TL.Chat chat => $"chat:{chat.id}",
        Channel channel => $"channel:{channel.id}",
        User user => $"user:{user.id}",
        _ => null
    };

    private void LoadTelegramFolderMap()
    {
        try
        {
            var path = TelegramFolderMapPath;
            if (File.Exists(path))
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(path));
                if (saved is not null)
                    foreach (var pair in saved)
                        _folderParents[pair.Key] = pair.Value;
            }
        }
        catch
        {
            // A metadata local corrompida não deve impedir a abertura do aplicativo.
        }
    }

    private string TelegramFolderMapPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive",
        "telegram-folders.json");

    private void SaveTelegramFolderMap()
    {
        try
        {
            var path = TelegramFolderMapPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_folderParents, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = $"A pasta foi criada, mas não foi possível salvar sua organização local: {ex.Message}";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    private void CloudFileRow_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        foreach (var button in row.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is TelegramFileItem))
        {
            var item = (TelegramFileItem)button.Tag!;
            button.IsVisible = button.Content?.ToString() != "⇩" || !item.IsFolder;
        }
    }

    private void CloudFileRow_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Border row)
            return;

        var isSelected = ReferenceEquals(CloudFilesList.SelectedItem, row.DataContext);
        foreach (var button in row.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is TelegramFileItem))
        {
            var item = (TelegramFileItem)button.Tag!;
            button.IsVisible = isSelected && (button.Content?.ToString() != "⇩" || !item.IsFolder);
        }
    }

    private async void CloudFilesList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (CloudFilesList.SelectedItem is not TelegramFileItem file)
            return;

        if (file.IsFolder)
        {
            NavigateToFolder(file);
            return;
        }

        ShowTelegramFileOptions(file);
    }

    private void DownloadTelegramFile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TelegramFileItem file } || file.IsFolder)
            return;
        if (_telegramClient is null)
        {
            TelegramFilesStatus.Text = "Conecte o Telegram antes de baixar arquivos.";
            return;
        }

        _pendingDownloadFile = file;
        DownloadFileName.Text = file.DisplayName;
        var folder = !string.IsNullOrWhiteSpace(_defaultDownloadFolder) && Directory.Exists(_defaultDownloadFolder)
            ? _defaultDownloadFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        DownloadFolderPath.Text = folder;
        DownloadVerifyHashCheckBox.IsChecked = VerifyDownloadHash.IsChecked == true;
        _downloadDecryptMode = file.DisplayName.EndsWith(".odenc", StringComparison.OrdinalIgnoreCase);
        DownloadEncryptCheckBox.Content = _downloadDecryptMode ? "Descriptografar pacote OmniDrive (.odenc)" : "Compactar e criptografar o download";
        DownloadEncryptCheckBox.IsChecked = false;
        DownloadEncryptionFields.IsVisible = false;
        DownloadEncryptionPassword.Text = "";
        DownloadEncryptionPasswordConfirm.Text = "";
        DownloadModalSubtitle.Text = file.IsSplitFile
            ? $"Arquivo dividido em {file.ExpectedPartCount} partes. O OmniDrive verificará a disponibilidade antes de reconstruí-lo."
            : "O arquivo será baixado do serviço de origem.";
        DownloadModalStatus.Text = file.IsSplitFile && file.IsIncomplete
            ? $"Aviso: conjunto marcado como incompleto ({file.PartsLabel}). O download só será concluído se todas as partes forem encontradas."
            : "";
        DownloadModalOverlay.IsVisible = true;
        DownloadFileName.Focus();
    }

    private async void ChooseDownloadFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolha a pasta para salvar o arquivo",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) DownloadFolderPath.Text = path;
    }

    private async void SelectDefaultDownloadFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Definir pasta de download padrão",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        _defaultDownloadFolder = path;
        DefaultDownloadFolder.Text = path;
        SaveSettings();
    }

    private void ClearDefaultDownloadFolder_Click(object? sender, RoutedEventArgs e)
    {
        _defaultDownloadFolder = null;
        DefaultDownloadFolder.Text = "";
        SaveSettings();
    }

    private void DownloadModalOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == DownloadModalOverlay) CancelDownload_Click(sender, e);
    }

    private void DownloadModalCard_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void CancelDownload_Click(object? sender, RoutedEventArgs e)
    {
        DownloadModalOverlay.IsVisible = false;
        _pendingDownloadFile = null;
    }

    private static string ExplainTransferError(Exception ex, string operation, string? credential = null)
    {
        var text = ex.ToString().ToLowerInvariant();
        var prefix = string.IsNullOrWhiteSpace(credential) ? "" : $"Conta “{credential}”: ";
        if (text.Contains("auth_key_unregistered") || text.Contains("session_revoked") || text.Contains("user_deactivated") || text.Contains("auth_key_invalid") || text.Contains("401") || text.Contains("unauthorized") || text.Contains("not authorized") || text.Contains("session expired"))
            return prefix + "a autenticação foi recusada ou expirou. Reconecte o serviço em Configurações → Conectar serviços.";
        if (text.Contains("disk full") || text.Contains("no space left") || text.Contains("not enough space") || text.Contains("there is not enough space") || text.Contains("0x80070070"))
            return prefix + "não há espaço livre suficiente no destino ou na unidade temporária. Libere espaço e tente novamente.";
        if (text.Contains("quota") || text.Contains("storage limit") || text.Contains("peer_flood") || text.Contains("file_parts_invalid") || text.Contains("file_part") || text.Contains("too large") || text.Contains("limit exceeded") || text.Contains("premium_account_required"))
            return prefix + "o serviço recusou a operação por limite, cota ou tamanho. Confira o limite do destino e as configurações de upload.";
        if (text.Contains("network") || text.Contains("connection") || text.Contains("timeout") || text.Contains("socket") || text.Contains("temporarily unavailable"))
            return prefix + "a conexão com o serviço foi interrompida. Verifique a internet e tente novamente.";
        return $"{prefix}falha ao {operation}: {ex.Message}";
    }

    private void MarkTelegramDisconnected(string reason)
    {
        _telegramClient?.Dispose();
        _telegramClient = null;
        _telegramPendingKind = null;
        TelegramStatus.Text = $"Telegram desconectado: {reason} Reconecte em Configurações → Conectar serviços.";
        TelegramConnectButton.Content = "Salvar e conectar";
    }
    private async void StartDownload_Click(object? sender, RoutedEventArgs e)
    {
        var file = _pendingDownloadFile;
        if (file is null || _telegramClient is null) return;
        var fileName = (DownloadFileName.Text ?? "").Trim();
        var folder = (DownloadFolderPath.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            DownloadModalStatus.Text = "Informe um nome de arquivo válido, sem caminho ou caracteres inválidos.";
            return;
        }
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            DownloadModalStatus.Text = "Escolha uma pasta de destino válida.";
            return;
        }
        if (file.IsSplitFile && file.IsIncomplete)
        {
            DownloadModalStatus.Text = $"Não é possível reconstruir o arquivo: faltam partes ({file.PartsLabel}).";
            return;
        }

        var verifyHash = DownloadVerifyHashCheckBox.IsChecked == true;
        var encryptOrDecryptDownload = DownloadEncryptCheckBox.IsChecked == true;
        var encryptionPassword = DownloadEncryptionPassword.Text ?? "";
        if (encryptOrDecryptDownload && string.IsNullOrWhiteSpace(encryptionPassword))
        {
            DownloadModalStatus.Text = "Informe a senha para continuar.";
            return;
        }
        if (encryptOrDecryptDownload && !_downloadDecryptMode
            && !string.Equals(encryptionPassword, DownloadEncryptionPasswordConfirm.Text ?? "", StringComparison.Ordinal))
        {
            DownloadModalStatus.Text = "As senhas não coincidem.";
            return;
        }
        if (encryptOrDecryptDownload && _downloadDecryptMode && !file.DisplayName.EndsWith(".odenc", StringComparison.OrdinalIgnoreCase))
        {
            DownloadModalStatus.Text = "O arquivo selecionado não é um pacote criptografado do OmniDrive.";
            return;
        }
        if (encryptOrDecryptDownload && !_downloadDecryptMode && !fileName.EndsWith(".odenc", StringComparison.OrdinalIgnoreCase))
            fileName += ".zip.odenc";
        if (encryptOrDecryptDownload && _downloadDecryptMode && fileName.EndsWith(".odenc", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^".odenc".Length];
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!);
            if (drive.IsReady && file.SizeBytes > 0 && drive.AvailableFreeSpace < file.SizeBytes)
            {
                DownloadModalStatus.Text = $"Espaço insuficiente: o arquivo precisa de aproximadamente {FormatFileSize(file.SizeBytes)} e há {FormatFileSize(drive.AvailableFreeSpace)} livres.";
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            DownloadModalStatus.Text = "Não foi possível verificar o espaço livre da pasta escolhida. Confirme as permissões e o espaço disponível antes de continuar.";
            return;
        }
        string outputPath = Path.Combine(folder, fileName);
        if (File.Exists(outputPath))
        {
            DownloadModalStatus.Text = "Já existe um arquivo com esse nome nessa pasta. Escolha outro nome para evitar sobrescrever dados.";
            return;
        }

        var queueItem = new TransferQueueItem
        {
            Icon = "⇩",
            FileName = fileName,
            PartDescription = file.IsSplitFile ? $"0/{file.ExpectedPartCount} partes" : "Preparando download",
            Service = "Telegram",
            Credential = file.ChatName,
            Status = "Aguardando",
            ProgressLabel = "Aguardando",
            Size = file.SizeLabel,
            Elapsed = "0 s"
        };
        TransferQueue.Add(queueItem);
        TransferQueuePanel.IsVisible = true;
        UpdateTransferQueueDisplay();
        DownloadModalOverlay.IsVisible = false;
        StartDownloadButton.IsEnabled = false;
        var started = DateTime.UtcNow;
        var tempOutputPath = outputPath + $".{Guid.NewGuid():N}.download";
        try
        {
            queueItem.Status = "Em andamento";
            queueItem.ProgressLabel = "Localizando arquivo";
            queueItem.Percentage = 3;
            var documents = await GetTelegramDocumentsForFileAsync(file);
            var orderedParts = file.IsSplitFile ? file.Parts.OrderBy(part => part.PartNumber).ToList() : new List<TelegramFilePart>();
            var cacheIdentity = file.IsSplitFile ? file.SplitUuid! : $"{file.ChatName}_{file.MessageId}_{file.Name}";
            var cacheKey = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cacheIdentity)));
            var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDrive", "download-cache", cacheKey);
            Directory.CreateDirectory(cacheDirectory);
            await using (var output = new FileStream(tempOutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                for (var index = 0; index < documents.Count; index++)
                {
                    var partNumber = file.IsSplitFile ? orderedParts[index].PartNumber : 1;
                    var expectedHash = file.IsSplitFile ? orderedParts[index].SavedHash : file.SavedHash;
                    var canCache = !string.IsNullOrWhiteSpace(expectedHash);
                    var cachedPartPath = Path.Combine(cacheDirectory, $"part_{partNumber:D6}.bin");
                    var tempPartPath = canCache ? cachedPartPath : Path.Combine(Path.GetTempPath(), $"OmniDrive_download_{Guid.NewGuid():N}.part");
                    try
                    {
                        queueItem.PartDescription = file.IsSplitFile ? $"Parte {partNumber}/{file.ExpectedPartCount}" : "Arquivo único";
                        queueItem.ProgressLabel = file.IsSplitFile
                            ? $"Baixando parte {partNumber}/{file.ExpectedPartCount}"
                            : "Baixando arquivo";
                        queueItem.Percentage = Math.Min(95, 5 + (double)index / Math.Max(1, documents.Count) * 90);

                        var useCachedPart = canCache && File.Exists(cachedPartPath)
                            && string.Equals(await ComputeSha256Async(cachedPartPath), expectedHash, StringComparison.OrdinalIgnoreCase);
                        if (useCachedPart)
                        {
                            queueItem.ProgressLabel = $"Reutilizando parte validada {partNumber}/{file.ExpectedPartCount}";
                        }
                        else
                        {
                            if (File.Exists(tempPartPath)) File.Delete(tempPartPath);
                            var bytesBeforePart = file.IsSplitFile
                                ? orderedParts.Take(index).Sum(part => Math.Max(0, part.SizeBytes))
                                : 0L;
                            await DownloadPartWithIntegrityCheckAsync(
                                documents[index], tempPartPath, expectedHash, partNumber, file.DisplayName, verifyHash,
                                bytes =>
                                {
                                    var totalBytes = bytesBeforePart + bytes;
                                    var elapsed = DateTime.UtcNow - started;
                                    var speed = elapsed.TotalSeconds > 0 ? totalBytes / elapsed.TotalSeconds : 0d;
                                    queueItem.Speed = "Velocidade: " + FormatFileSize((long)Math.Max(0, speed)) + "/s";
                                    queueItem.Elapsed = FormatElapsed(elapsed);
                                    if (file.SizeBytes > 0 && speed > 0)
                                    {
                                        var remainingSeconds = Math.Max(0, (file.SizeBytes - totalBytes) / speed);
                                        queueItem.Remaining = "Restante: " + FormatElapsed(TimeSpan.FromSeconds(Math.Min(remainingSeconds, 31536000d)));
                                        queueItem.Percentage = Math.Clamp((double)totalBytes / file.SizeBytes * 100d, 0d, 99d);
                                    }
                                });
                        }

                        await using var partStream = File.OpenRead(tempPartPath);
                        await partStream.CopyToAsync(output);
                    }
                    finally
                    {
                        if (!canCache)
                            try { if (File.Exists(tempPartPath)) File.Delete(tempPartPath); } catch { }
                    }
                    queueItem.Percentage = Math.Min(98, (double)(index + 1) / documents.Count * 98);
                    queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - started);
                }
            }
            if (encryptOrDecryptDownload)
            {
                var transformedPath = tempOutputPath + (_downloadDecryptMode ? ".decrypted" : ".encrypted");
                try
                {
                    if (_downloadDecryptMode)
                        await FileEncryptionService.DecryptFileAsync(tempOutputPath, transformedPath, encryptionPassword);
                    else
                    {
                        var zipPath = tempOutputPath + ".zip";
                        try
                        {
                            await Task.Run(() =>
                            {
                                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                                archive.CreateEntryFromFile(tempOutputPath, file.DisplayName, CompressionLevel.Optimal);
                            });
                            await FileEncryptionService.EncryptFileAsync(zipPath, transformedPath, encryptionPassword);
                        }
                        finally { try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { } }
                    }
                    File.Delete(tempOutputPath);
                    File.Move(transformedPath, tempOutputPath);
                }
                finally { try { if (File.Exists(transformedPath)) File.Delete(transformedPath); } catch { } }
            }
            File.Move(tempOutputPath, outputPath);
            try { if (Directory.Exists(cacheDirectory)) Directory.Delete(cacheDirectory, recursive: true); } catch { }
            queueItem.Percentage = 100;
            queueItem.Status = "Concluído";
            queueItem.CanRetry = false;
            queueItem.ProgressLabel = "Download concluído";
            queueItem.PartDescription = "Concluído";
            queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - started);
            TelegramFilesStatus.Text = file.IsSplitFile
                ? $"Arquivo reconstruído com {documents.Count} partes: {outputPath}"
                : $"Download concluído: {outputPath}";
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath); } catch { }
            var explanation = ExplainTransferError(ex, "baixar", file.ChatName);
            queueItem.Status = "Falha";
            queueItem.ProgressLabel = explanation;
            queueItem.PartDescription = file.IsSplitFile ? $"Falha no conjunto · {queueItem.PartDescription}" : "Falha no arquivo";
            queueItem.CanRetry = true;
            queueItem.RetryAction = () =>
            {
                DownloadTelegramFileForItem(file);
                return Task.CompletedTask;
            };
            queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - started);
            TelegramFilesStatus.Text = $"Não foi possível baixar “{file.DisplayName}”: {explanation}";
            DownloadModalStatus.Text = TelegramFilesStatus.Text;
            if (explanation.Contains("autenticação", StringComparison.OrdinalIgnoreCase))
                MarkTelegramDisconnected(explanation);
        }
        finally
        {
            StartDownloadButton.IsEnabled = true;
            _pendingDownloadFile = null;
            UpdateTransferQueueDisplay();
        }
    }

    private void TelegramFileOptions_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TelegramFileItem file })
            ShowTelegramFileOptions(file);
    }

    private void SelectAllCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (SelectAllCheckBox.IsChecked == true)
        {
            foreach (var item in CloudFilesList.Items.OfType<TelegramFileItem>())
                CloudFilesList.SelectedItems.Add(item);
        }
        else
        {
            CloudFilesList.SelectedItems.Clear();
        }
    }

    private void ShowTelegramFileOptions(TelegramFileItem file)
    {
        var menu = new ContextMenu();
        if (file.IsFolder)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "Abrir pasta",
                Command = new RelayUiCommand(() => NavigateToFolder(file))
            });
            menu.Open(CloudFilesList);
            return;
        }

        menu.Items.Add(new MenuItem
        {
            Header = "Download",
            Command = new RelayUiCommand(() => DownloadTelegramFileForItem(file))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Renomear",
            Command = new RelayUiCommand(() => _ = RenameTelegramFileAsync(file))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Mover",
            Command = new RelayUiCommand(() => _ = MoveTelegramFileAsync(file))
        });
        menu.Items.Add(new MenuItem
        {
            Header = file.IsFavorite ? "Remover dos favoritos" : "Favoritar",
            Command = new RelayUiCommand(() => _ = ToggleFavoriteAsync(file))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Excluir",
            Command = new RelayUiCommand(() => _ = DeleteTelegramFileAsync(file))
        });
        menu.Open(CloudFilesList);
    }

    private string GetFavoriteKey(TelegramFileItem file)
    {
        if (file.IsSplitFile && !string.IsNullOrWhiteSpace(file.SplitUuid))
            return "telegram:split:" + file.SplitUuid;
        return "telegram:" + GetDeletePeerKey(file.Peer) + ":" + file.MessageId;
    }

    private void LoadFavoriteKeys()
    {
        try
        {
            if (File.Exists(_favoritesPath))
            {
                var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_favoritesPath));
                if (keys is not null)
                    foreach (var key in keys)
                        _favoriteKeys.Add(key);
            }
        }
        catch { }
    }

    private void SaveFavoriteKeys()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_favoritesPath)!);
            File.WriteAllText(_favoritesPath, JsonSerializer.Serialize(_favoriteKeys.OrderBy(key => key).ToList(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = "Não foi possível salvar os favoritos: " + ex.Message;
        }
    }

    private async Task ToggleFavoriteAsync(TelegramFileItem file)
    {
        var key = GetFavoriteKey(file);
        if (!_favoriteKeys.Add(key))
            _favoriteKeys.Remove(key);
        SaveFavoriteKeys();
        await LoadTelegramFilesAsync();
        TelegramFilesStatus.Text = _favoriteKeys.Contains(key)
            ? $"“{file.DisplayName}” foi adicionado aos favoritos."
            : $"“{file.DisplayName}” foi removido dos favoritos.";
    }

    private async Task MoveTelegramFileAsync(TelegramFileItem file)
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte a credencial do Telegram antes de mover arquivos.";
            return;
        }

        var folders = _loadedCloudFiles.Where(item => item.IsFolder && !string.Equals(item.FolderKey, GetDeletePeerKey(file.Peer), StringComparison.Ordinal)).ToList();
        if (folders.Count == 0)
        {
            TelegramFilesStatus.Text = "Não há pastas de destino nessa credencial. Crie uma pasta primeiro.";
            return;
        }

        var picker = new ComboBox { MinWidth = 320, ItemsSource = folders, SelectedIndex = 0 };
        picker.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TelegramFileItem>(
            (item, _) => new TextBlock { Text = item.DisplayName },
            supportsRecycling: false);
        var description = new TextBlock
        {
            Text = "Selecione uma pasta da mesma credencial do arquivo. O OmniDrive encaminhará os itens para a pasta escolhida e, após confirmar o envio, removerá as mensagens de origem.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brush.Parse("#F4F0FA")
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        var dialog = new Window
        {
            Title = "Mover arquivo",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brush.Parse("#201D28"),
            Padding = new Thickness(22)
        };
        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(description);
        body.Children.Add(picker);
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 };
        cancel.Click += (_, _) => dialog.Close(false);
        var move = new Button { Content = "Mover", MinWidth = 90 };
        move.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(move);
        body.Children.Add(buttons);
        dialog.Content = body;

        if (await dialog.ShowDialog<bool>(this) != true || picker.SelectedItem is not TelegramFileItem destination)
            return;

        var parts = file.IsSplitFile
            ? file.Parts.OrderBy(part => part.PartNumber).Select(part => (part.Peer, part.MessageId)).ToList()
            : new List<(InputPeer Peer, int MessageId)> { (file.Peer, file.MessageId) };

        try
        {
            foreach (var group in parts.GroupBy(part => GetDeletePeerKey(part.Peer), StringComparer.Ordinal))
            {
                var groupParts = group.ToList();
                var sourcePeer = groupParts[0].Peer;
                var ids = groupParts.Select(part => part.MessageId).ToArray();
                var randomIds = ids.Select(_ => Random.Shared.NextInt64(1, long.MaxValue)).ToArray();
                await _telegramClient.Messages_ForwardMessages(sourcePeer, ids, randomIds, destination.Peer);
            }

            await DeleteTelegramFileFromSourceAsync(file);
            await LoadTelegramFilesAsync();
            TelegramFilesStatus.Text = $"“{file.DisplayName}” foi movido para “{destination.DisplayName}”.";
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = $"Não foi possível concluir a movimentação: {ex.Message}. Confira as pastas de origem e destino antes de tentar novamente.";
        }
    }

    private async Task RenameTelegramFileAsync(TelegramFileItem file)
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte o Telegram antes de renomear arquivos.";
            return;
        }
        if (file.IsFolder || (file.IsSplitFile && file.IsIncomplete))
        {
            TelegramFilesStatus.Text = "Não é possível renomear uma pasta ou um arquivo dividido incompleto.";
            return;
        }

        var nameBox = new TextBox
        {
            Text = file.DisplayName,
            MinWidth = 360,
            Watermark = "Novo nome do arquivo"
        };
        var dialog = new Window
        {
            Title = "Renomear arquivo",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brush.Parse("#201D28"),
            Padding = new Thickness(22)
        };
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = file.IsSplitFile
                ? "O nome de cada parte será atualizado no serviço de origem, preservando os metadados individuais. Os arquivos não serão baixados nem reenviados."
                : "O nome será atualizado no serviço de origem. O arquivo não será baixado nem reenviado.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brush.Parse("#F4F0FA")
        });
        panel.Children.Add(nameBox);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 };
        cancel.Click += (_, _) => dialog.Close((string?)null);
        var rename = new Button { Content = "Renomear", MinWidth = 90 };
        rename.Click += (_, _) => dialog.Close(nameBox.Text?.Trim());
        buttons.Children.Add(cancel);
        buttons.Children.Add(rename);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        var newName = await dialog.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(newName) || newName == file.DisplayName)
            return;
        newName = Path.GetFileName(newName.Trim());
        if (string.IsNullOrWhiteSpace(newName) || newName is "." or "..")
        {
            TelegramFilesStatus.Text = "Informe um nome de arquivo válido.";
            return;
        }
        if (!await ConfirmActionAsync(
                "Confirmar renomeação",
                $"O nome do arquivo será alterado para “{newName}” no serviço de origem. Nenhum arquivo será reenviado. Continuar?"))
            return;

        try
        {
            var sourceParts = new List<(InputPeer Peer, int MessageId, string SourceName, int PartNumber, int TotalParts)>();
            if (file.IsSplitFile)
            {
                foreach (var part in file.Parts.OrderBy(part => part.PartNumber))
                    sourceParts.Add((part.Peer, part.MessageId, part.Name, part.PartNumber, part.TotalParts));
            }
            else
            {
                sourceParts.Add((file.Peer, file.MessageId, file.Name, 1, 1));
            }

            for (var index = 0; index < sourceParts.Count; index++)
            {
                var part = sourceParts[index];
                var history = await _telegramClient.Messages_GetHistory(part.Peer, limit: 100);
                var message = history.Messages.OfType<Message>().FirstOrDefault(item => item.ID == part.MessageId);
                if (message?.media is not MessageMediaDocument { document: Document })
                    throw new InvalidOperationException($"A parte {part.PartNumber}/{part.TotalParts} não foi encontrada no Telegram.");

                var captionName = newName;
                if (file.IsSplitFile)
                {
                    if (!TryParseSplitFileName(part.SourceName, out var parsed))
                        throw new InvalidOperationException("Não foi possível validar os metadados de divisão do arquivo.");
                    captionName = $"{Path.GetFileNameWithoutExtension(newName)}_OD[{parsed.Uuid}_{part.PartNumber}-{part.TotalParts}]{Path.GetExtension(newName)}";
                }

                var newCaption = RenameTelegramCaption(message.message, part.SourceName, captionName);
                TelegramFilesStatus.Text = file.IsSplitFile
                    ? $"Atualizando caption da parte {index + 1}/{sourceParts.Count}..."
                    : "Atualizando caption no Telegram...";
                await _telegramClient.Messages_EditMessage(part.Peer, message.ID, newCaption);
            }

            await LoadTelegramFilesAsync();
            TelegramFilesStatus.Text = $"Arquivo renomeado no Telegram para “{newName}”.";
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = $"Não foi possível concluir a renomeação: {ex.Message}. Se o arquivo tiver várias partes, confira as captions: o Telegram pode ter aceitado algumas alterações antes da falha.";
        }
    }

    private async Task DeleteTelegramFileAsync(TelegramFileItem file)
    {
        await DeleteTelegramFilesAsync(new[] { file });
    }

    private async void DownloadSelected_Click(object? sender, RoutedEventArgs e) => await DownloadSelectedTelegramFilesAsync();

    private async Task DownloadSelectedTelegramFilesAsync()
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte o Telegram antes de baixar arquivos.";
            return;
        }

        var selected = CloudFilesList.SelectedItems.OfType<TelegramFileItem>().ToList();
        var files = selected.Where(file => !file.IsFolder).ToList();
        if (files.Count == 0)
        {
            TelegramFilesStatus.Text = "Selecione um ou mais arquivos para baixar.";
            return;
        }

        var incomplete = files.Where(file => file.IsSplitFile && file.IsIncomplete).ToList();
        files = files.Except(incomplete).ToList();
        if (files.Count == 0)
        {
            TelegramFilesStatus.Text = "Os arquivos selecionados estão incompletos e não podem ser reconstruídos.";
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolha a pasta para salvar os arquivos selecionados",
            AllowMultiple = false
        });
        if (folders.Count == 0)
            return;

        var destination = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destination))
        {
            TelegramFilesStatus.Text = "Não foi possível obter o caminho da pasta escolhida.";
            return;
        }

        var succeeded = 0;
        var failures = new List<string>();
        foreach (var file in files)
        {
            var outputPath = GetUniqueDownloadPath(destination, file.DisplayName);
            try
            {
                TelegramFilesStatus.Text = $"Baixando {succeeded + 1}/{files.Count}: {file.DisplayName}...";
                var documents = await GetTelegramDocumentsForFileAsync(file);
                var orderedParts = file.IsSplitFile ? file.Parts.OrderBy(part => part.PartNumber).ToList() : new List<TelegramFilePart>();
                await using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    for (var index = 0; index < documents.Count; index++)
                    {
                        var partNumber = file.IsSplitFile ? orderedParts[index].PartNumber : 1;
                        var expectedHash = file.IsSplitFile ? orderedParts[index].SavedHash : file.SavedHash;
                        var tempPartPath = Path.Combine(Path.GetTempPath(), $"OmniDrive_download_{Guid.NewGuid():N}.part");
                        try
                        {
                            await DownloadPartWithIntegrityCheckAsync(documents[index], tempPartPath, expectedHash, partNumber, file.DisplayName);
                            await using var partStream = File.OpenRead(tempPartPath);
                            await partStream.CopyToAsync(output);
                        }
                        finally
                        {
                            try { if (File.Exists(tempPartPath)) File.Delete(tempPartPath); } catch { }
                        }
                    }
                }
                succeeded++;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                failures.Add($"{file.DisplayName}: {ex.Message}");
            }
        }

        var summary = $"Downloads concluídos: {succeeded}/{files.Count}. Pasta: {destination}";
        if (incomplete.Count > 0)
            summary += $" Arquivos divididos incompletos ignorados: {incomplete.Count}.";
        if (selected.Count != files.Count + incomplete.Count)
            summary += $" Pastas ignoradas: {selected.Count - files.Count - incomplete.Count}.";
        if (failures.Count > 0)
            summary += " Falhas: " + string.Join(" | ", failures.Take(3));
        TelegramFilesStatus.Text = summary;
    }

    private async Task<List<Document>> GetTelegramDocumentsForFileAsync(TelegramFileItem file)
    {
        if (_telegramClient is null)
            throw new InvalidOperationException("O cliente do Telegram não está disponível.");

        var documents = new List<Document>();
        if (file.IsSplitFile)
        {
            foreach (var part in file.Parts.OrderBy(part => part.PartNumber))
            {
                var history = await _telegramClient.Messages_GetHistory(part.Peer, limit: 100);
                var message = history.Messages.OfType<Message>().FirstOrDefault(item => item.ID == part.MessageId);
                if (message?.media is not MessageMediaDocument { document: Document document })
                    throw new InvalidOperationException($"A parte {part.PartNumber}/{part.TotalParts} não foi encontrada.");
                documents.Add(document);
            }
        }
        else
        {
            var history = await _telegramClient.Messages_GetHistory(file.Peer, limit: 100);
            var message = history.Messages.OfType<Message>().FirstOrDefault(item => item.ID == file.MessageId);
            if (message?.media is not MessageMediaDocument { document: Document document })
                throw new InvalidOperationException("A mensagem original do Telegram não contém mais este arquivo.");
            documents.Add(document);
        }

        return documents;
    }

    private static string GetUniqueDownloadPath(string folder, string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "arquivo";
        var path = Path.Combine(folder, safeName);
        if (!File.Exists(path))
            return path;

        var name = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);
        for (var index = 2; ; index++)
        {
            path = Path.Combine(folder, $"{name} ({index}){extension}");
            if (!File.Exists(path))
                return path;
        }
    }

    private async void DeleteSelected_Click(object? sender, RoutedEventArgs e) => await DeleteSelectedTelegramFilesAsync();

    private async Task DeleteSelectedTelegramFilesAsync()
    {
        var selected = CloudFilesList.SelectedItems.OfType<TelegramFileItem>().ToList();
        await DeleteTelegramFilesAsync(selected);
    }

    private async Task DeleteTelegramFilesAsync(IReadOnlyCollection<TelegramFileItem> requestedFiles)
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte o Telegram antes de excluir arquivos.";
            return;
        }

        var files = requestedFiles.Where(file => !file.IsFolder).Distinct().ToList();
        if (files.Count == 0)
        {
            TelegramFilesStatus.Text = "Selecione um ou mais arquivos. Pastas não podem ser excluídas por esta ação.";
            return;
        }

        var foldersSkipped = requestedFiles.Count - files.Count;
        var description = files.Count == 1
            ? $"excluir “{files[0].DisplayName}”"
            : $"excluir {files.Count} arquivos selecionados";
        if (!await ConfirmActionAsync(
                "Confirmar exclusão",
                $"O OmniDrive vai {description} diretamente do Telegram. Arquivos divididos serão excluídos com suas partes associadas ao UUID. Esta ação pode não ser reversível. Deseja continuar?"))
            return;

        var deleted = 0;
        var failures = new List<string>();
        foreach (var file in files)
        {
            try
            {
                await DeleteTelegramFileFromSourceAsync(file);
                deleted++;
            }
            catch (Exception ex)
            {
                failures.Add($"{file.DisplayName}: {ex.Message}");
            }
        }

        if (deleted > 0)
            await LoadTelegramFilesAsync();

        var summary = $"Excluídos do Telegram: {deleted}/{files.Count}.";
        if (foldersSkipped > 0)
            summary += $" Pastas ignoradas: {foldersSkipped}.";
        if (failures.Count > 0)
            summary += " Falhas: " + string.Join(" | ", failures.Take(3));
        TelegramFilesStatus.Text = summary;
    }

    private async Task DeleteTelegramFileFromSourceAsync(TelegramFileItem file)
    {
        if (_telegramClient is null)
            throw new InvalidOperationException("O cliente do Telegram não está disponível.");

        if (file.IsFolder)
            throw new InvalidOperationException("Pastas não podem ser excluídas por esta ação.");

        var messages = file.IsSplitFile
            ? file.Parts.Select(part => (part.Peer, part.MessageId)).ToList()
            : new List<(InputPeer Peer, int MessageId)> { (file.Peer, file.MessageId) };

        var validMessages = messages.Where(item => item.MessageId > 0).ToList();
        if (validMessages.Count == 0)
            throw new InvalidOperationException("Não foram encontrados IDs de mensagens válidos para este item.");

        foreach (var group in validMessages.GroupBy(item => GetDeletePeerKey(item.Peer), StringComparer.Ordinal))
        {
            var first = group.First();
            var ids = group.Select(item => item.MessageId).Distinct().ToArray();
            if (first.Peer is InputPeerChannel channelPeer)
            {
                await _telegramClient.Channels_DeleteMessages(
                    new InputChannel(channelPeer.channel_id, channelPeer.access_hash), ids);
            }
            else
            {
                await _telegramClient.Messages_DeleteMessages(ids, revoke: true);
            }
        }
    }

    private static string GetDeletePeerKey(InputPeer peer) => peer switch
    {
        InputPeerChannel channel => $"channel:{channel.channel_id}",
        InputPeerChat chat => $"chat:{chat.chat_id}",
        InputPeerUser user => $"user:{user.user_id}",
        InputPeerSelf => "self",
        _ => peer.GetType().Name
    };

    private async Task<bool> ConfirmActionAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brush.Parse("#201D28"),
            Padding = new Thickness(22)
        };

        var content = new StackPanel { Spacing = 18 };
        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brush.Parse("#F4F0FA")
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 };
        cancel.Click += (_, _) => dialog.Close(false);
        var confirm = new Button { Content = "Excluir", MinWidth = 90 };
        confirm.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        content.Children.Add(buttons);
        dialog.Content = content;

        return await dialog.ShowDialog<bool>(this);
    }

    private void DownloadTelegramFileForItem(TelegramFileItem file)
    {
        DownloadTelegramFile_Click(new Button { Tag = file }, new RoutedEventArgs());
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = "Criar pasta",
            Command = new RelayUiCommand(() => _ = CreateTelegramFolderAsync())
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Upload de arquivo",
            // Defer the native picker until the context menu has closed.
            Command = new RelayUiCommand(() =>
                Dispatcher.UIThread.Post(async () => await ChooseUploadFilesAsync(), DispatcherPriority.Background))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Upload de pasta",
            // Defer the native picker until the context menu has closed.
            Command = new RelayUiCommand(() =>
                Dispatcher.UIThread.Post(async () => await ChooseUploadFolderAsync(), DispatcherPriority.Background))
        });

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        button.ContextMenu = menu;
        menu.Open(button);
    }

    private async Task ChooseUploadFilesAsync()
    {
        try
        {
            UploadModalTitle.Text = "Selecionar arquivos";
            UploadItemsPanel.Children.Clear();
            UploadModalStatus.Text = "Abrindo o seletor de arquivos do Windows...";
            UploadModalOverlay.IsVisible = true;
            await Task.Delay(50);
            UploadModalOverlay.IsVisible = false;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecione um ou mais arquivos para upload",
                AllowMultiple = true
            });
            if (files.Count == 0) return;

            await RefreshUploadDestinationsAsync();
            var defaultDestination = GetCurrentUploadDestination();
            _pendingUploads.Clear();
            _uploadIsFolder = false;
            bool lastZip = false;
            bool lastHash = VerifyUploadHash.IsChecked == true;
            foreach (var file in files)
            {
                var localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath))
                    throw new InvalidOperationException($"Não foi possível obter o caminho local do arquivo '{file.Name}'.");

                _pendingUploads.Add(new UploadEntry
                {
                    SourcePath = localPath,
                    OriginalName = Path.GetFileName(file.Name),
                    UploadName = Path.GetFileName(file.Name),
                    Credential = "Telegram",
                    Destination = defaultDestination,
                    VerifyHash = lastHash,
                    Compress = lastZip
                });
            }
            BuildUploadModal();
        }
        catch (Exception ex)
        {
            ShowUploadPickerError("arquivos", ex);
        }
    }

    private async Task ChooseUploadFolderAsync()
    {
        try
        {
            UploadModalTitle.Text = "Selecionar pasta";
            UploadItemsPanel.Children.Clear();
            UploadModalStatus.Text = "Abrindo o seletor de pastas do Windows...";
            UploadModalOverlay.IsVisible = true;
            await Task.Delay(50);
            UploadModalOverlay.IsVisible = false;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Selecione uma pasta para compactar e enviar",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("O seletor retornou uma pasta sem caminho local.");

            await RefreshUploadDestinationsAsync();
            var defaultDestination = GetCurrentUploadDestination();
            _pendingUploads.Clear();
            _uploadIsFolder = true;
            _pendingUploads.Add(new UploadEntry
            {
                SourcePath = path,
                OriginalName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                UploadName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + ".zip",
                Credential = "Telegram",
                Destination = defaultDestination,
                VerifyHash = VerifyUploadHash.IsChecked == true,
                Compress = true,
                IsFolder = true
            });
            BuildUploadModal();
        }
        catch (Exception ex)
        {
            ShowUploadPickerError("pastas", ex);
        }
    }

    private void ShowUploadPickerError(string kind, Exception ex)
    {
        _pendingUploads.Clear();
        UploadItemsPanel.Children.Clear();
        UploadModalTitle.Text = $"Falha ao abrir o seletor de {kind}";
        UploadModalStatus.Text = $"O Windows não conseguiu abrir o seletor de {kind}. Detalhes: {ex.GetType().Name}: {ex.Message}";
        UploadModalOverlay.IsVisible = true;
    }

    private void BuildUploadModal()
    {
        UploadItemsPanel.Children.Clear();
        UploadModalTitle.Text = _uploadIsFolder ? "Upload de pasta" : $"Upload de arquivos ({_pendingUploads.Count})";
        UploadModalStatus.Text = "";
        var credentials = new[] { "Telegram" };
        foreach (var entry in _pendingUploads)
        {
            entry.Destination ??= GetCurrentUploadDestination();
            var card = new Border { Classes = { "panel" }, Padding = new Thickness(14) };
            var fields = new StackPanel { Spacing = 9 };
            var nameBox = new TextBox { Text = entry.UploadName, Watermark = "Nome no destino", Width = 420, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            nameBox.TextChanged += (_, _) => entry.UploadName = nameBox.Text?.Trim() ?? "";
            fields.Children.Add(new TextBlock { Text = "Nome no destino", Classes = { "muted" }, FontSize = 11 });
            fields.Children.Add(nameBox);
            var destinationFields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            var credentialField = new StackPanel { Spacing = 5 };
            credentialField.Children.Add(new TextBlock { Text = "Credencial de destino", Classes = { "muted" }, FontSize = 11 });
            var credentialBox = new ComboBox { ItemsSource = credentials, SelectedItem = entry.Credential, MinWidth = 180 };
            credentialField.Children.Add(credentialBox);
            destinationFields.Children.Add(credentialField);
            var folderField = new StackPanel { Spacing = 5 };
            folderField.Children.Add(new TextBlock { Text = "Pasta de destino", Classes = { "muted" }, FontSize = 11 });
            var folderBox = new ComboBox { ItemsSource = GetDestinationsForCredential(entry.Credential), SelectedItem = entry.Destination, MinWidth = 240, MaxWidth = 360 };
            folderBox.SelectionChanged += (_, _) =>
            {
                if (folderBox.SelectedItem is UploadDestination destination)
                    entry.Destination = destination;
            };
            credentialBox.SelectionChanged += (_, _) =>
            {
                entry.Credential = credentialBox.SelectedItem?.ToString() ?? "Telegram";
                folderBox.ItemsSource = GetDestinationsForCredential(entry.Credential);
                var current = entry.Destination;
                entry.Destination = current is not null && _uploadDestinations.Any(destination => destination.Key == current.Key)
                    ? _uploadDestinations.First(destination => destination.Key == current.Key)
                    : GetCurrentUploadDestination();
                folderBox.SelectedItem = entry.Destination;
            };
            folderField.Children.Add(folderBox);
            destinationFields.Children.Add(folderField);
            fields.Children.Add(destinationFields);
            var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
            var hashCheck = new CheckBox { Content = "Verificar hash", IsChecked = entry.VerifyHash };
            hashCheck.IsCheckedChanged += (_, _) => entry.VerifyHash = hashCheck.IsChecked == true;
            options.Children.Add(hashCheck);
            if (!entry.IsFolder)
            {
                var zipCheck = new CheckBox { Content = "Compactar em ZIP", IsChecked = entry.Compress };
                zipCheck.IsCheckedChanged += (_, _) => entry.Compress = zipCheck.IsChecked == true;
                options.Children.Add(zipCheck);
            }
            var encryptionCheck = new CheckBox { Content = "Compactar e criptografar", IsChecked = entry.Encrypt };
            var encryptionFields = new StackPanel { Spacing = 6, IsVisible = entry.Encrypt };
            var passwordBox = new TextBox { PasswordChar = '●', Watermark = "Senha de criptografia", Width = 320, Text = entry.EncryptionPassword };
            passwordBox.TextChanged += (_, _) => entry.EncryptionPassword = passwordBox.Text ?? "";
            encryptionCheck.IsCheckedChanged += (_, _) =>
            {
                entry.Encrypt = encryptionCheck.IsChecked == true;
                encryptionFields.IsVisible = entry.Encrypt;
                if (entry.Encrypt)
                {
                    entry.Compress = true;
                    if (!entry.UploadName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        entry.UploadName = entry.UploadName + ".zip";
                    zipCheckIfPresent();
                }
            };
            encryptionFields.Children.Add(new TextBlock { Text = "Senha (não será salva)", Classes = { "muted" }, FontSize = 11 });
            encryptionFields.Children.Add(passwordBox);
            fields.Children.Add(encryptionCheck);
            fields.Children.Add(encryptionFields);
            void zipCheckIfPresent()
            {
                foreach (var child in options.Children.OfType<CheckBox>())
                    if (Equals(child.Content, "Compactar em ZIP")) child.IsChecked = entry.Compress;
            }
            fields.Children.Add(options);
            card.Child = fields;
            UploadItemsPanel.Children.Add(card);
        }
        UploadModalOverlay.IsVisible = true;
    }

    private void UploadModalOverlay_PointerPressed(object? sender, PointerPressedEventArgs e) => UploadModalOverlay.IsVisible = false;
    private void UploadModalCard_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
    private void CancelUpload_Click(object? sender, RoutedEventArgs e)
    {
        UploadModalOverlay.IsVisible = false;
        _pendingUploads.Clear();
    }

    private async void StartUpload_Click(object? sender, RoutedEventArgs e)
    {
        if (_telegramClient?.User is null || _pendingUploads.Count == 0)
        {
            UploadModalStatus.Text = "Não há arquivos prontos para enviar ou a credencial não está conectada.";
            return;
        }
        if (_pendingUploads.Any(entry => string.IsNullOrWhiteSpace(entry.UploadName)))
        {
            UploadModalStatus.Text = "Informe um nome válido para cada arquivo.";
            return;
        }
        if (_pendingUploads.Any(entry => entry.Encrypt && string.IsNullOrWhiteSpace(entry.EncryptionPassword)))
        {
            UploadModalStatus.Text = "Informe uma senha para cada arquivo que será criptografado.";
            return;
        }
        foreach (var entry in _pendingUploads)
            if (entry.Encrypt) entry.Compress = true;

        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        var uploadFailed = false;
        var queuedUploads = _pendingUploads.ToDictionary(entry => entry, entry => new TransferQueueItem
        {
            Icon = entry.IsFolder || entry.Compress ? "🗜" : "📄",
            FileName = entry.UploadName,
            PartDescription = "Na fila",
            Service = "Telegram",
            Credential = entry.Credential,
            Status = "Aguardando",
            ProgressLabel = "Aguardando",
            Size = File.Exists(entry.SourcePath) ? FormatFileSize(new FileInfo(entry.SourcePath).Length) : "—",
            Elapsed = "0 s"
        });
        foreach (var item in queuedUploads.Values) TransferQueue.Add(item);
        TransferQueuePanel.IsVisible = true;
        UpdateTransferQueueDisplay();
        UploadModalOverlay.IsVisible = false;
        try
        {
            foreach (var entry in _pendingUploads.ToList())
            {
                var queueItem = queuedUploads[entry];
                var transferStarted = DateTime.UtcNow;
                var uploadedParts = 0;
                queueItem.Status = "Em andamento";
                queueItem.ProgressLabel = "Preparando arquivo";
                queueItem.PartDescription = "Preparando arquivo";
                string? tempPath = null;
                string? encryptedTempPath = null;
                try
                {
                    var sourcePath = entry.SourcePath;
                    var uploadName = entry.UploadName;
                    if (entry.IsFolder || entry.Compress)
                    {
                        queueItem.ProgressLabel = entry.IsFolder ? "Calculando tamanho da pasta" : "Preparando compactação";
                        UploadModalStatus.Text = queueItem.ProgressLabel;
                        var estimatedBytes = entry.IsFolder
                            ? await Task.Run(() => Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length))
                            : new FileInfo(sourcePath).Length;
                        var tempDrive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!);
                        if (tempDrive.IsReady && estimatedBytes > 0 && tempDrive.AvailableFreeSpace < estimatedBytes)
                            throw new IOException($"No space left on temporary drive. Needed about {FormatFileSize(estimatedBytes)}; available {FormatFileSize(tempDrive.AvailableFreeSpace)}.");
                        tempPath = Path.Combine(Path.GetTempPath(), $"OmniDrive_{Guid.NewGuid():N}.zip");
                        if (entry.IsFolder)
                        {
                            ZipFile.CreateFromDirectory(sourcePath, tempPath, CompressionLevel.Optimal, includeBaseDirectory: true);
                        }
                        else
                        {
                            await Task.Run(() =>
                            {
                                using var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create);
                                archive.CreateEntryFromFile(sourcePath, Path.GetFileName(sourcePath), CompressionLevel.Optimal);
                            });
                        }
                        sourcePath = tempPath;
                        if (!uploadName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) uploadName += ".zip";
                    }

                    if (entry.Encrypt)
                    {
                        if (string.IsNullOrWhiteSpace(tempPath))
                            throw new InvalidOperationException("A criptografia exige que o arquivo seja compactado primeiro.");
                        encryptedTempPath = Path.Combine(Path.GetTempPath(), $"OmniDrive_{Guid.NewGuid():N}.odenc");
                        queueItem.ProgressLabel = "Criptografando pacote";
                        UploadModalStatus.Text = queueItem.ProgressLabel;
                        await FileEncryptionService.EncryptFileAsync(tempPath, encryptedTempPath, entry.EncryptionPassword);
                        try { File.Delete(tempPath); } catch { }
                        tempPath = encryptedTempPath;
                        encryptedTempPath = null;
                        sourcePath = tempPath;
                        if (!uploadName.EndsWith(".odenc", StringComparison.OrdinalIgnoreCase)) uploadName += ".odenc";
                    }

                    if (!File.Exists(sourcePath))
                        throw new FileNotFoundException("O arquivo local não foi encontrado.", sourcePath);

                    var destination = entry.Destination?.Peer ?? new InputPeerSelf();
                    var destinationName = entry.Destination?.Name ?? "Telegram";
                    var partLimit = GetEffectiveUploadPartSizeBytes(entry.Credential);
                    var sourceLength = new FileInfo(sourcePath).Length;
                    queueItem.FileName = uploadName;
                    queueItem.Size = FormatFileSize(sourceLength);
                    queueItem.Status = "Em andamento";
                    queueItem.ProgressLabel = "Preparando partes";
                    queueItem.PartDescription = "Preparando partes";
                    UploadModalStatus.Text = $"Preparando {uploadName} para envio...";

                    void ReportProgress(long transmitted, long totalSize)
                    {
                        var progress = totalSize > 0 ? Math.Clamp((double)transmitted / totalSize * 100d, 0d, 100d) : 0d;
                        Dispatcher.UIThread.Post(() =>
                        {
                            queueItem.Percentage = progress;
                            queueItem.ProgressLabel = $"Enviando · {progress:0}%";
                            queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - transferStarted);
                            queueItem.Status = "Em andamento";
                            UploadModalStatus.Text = $"Enviando {uploadName}: {progress:P0}";
                            if (ReferenceEquals(_selectedTransfer, queueItem)) RefreshTransferDetails(queueItem);
                        });
                    }

                    if (sourceLength > partLimit)
                    {
                        var splitUuid = Guid.NewGuid().ToString();
                        var partPaths = await SplitFileIntoTemporaryPartsAsync(sourcePath, partLimit, splitUuid);
                        try
                        {
                            for (var index = 0; index < partPaths.Count; index++)
                            {
                                var partNumber = index + 1;
                                var partName = BuildSplitPartName(uploadName, splitUuid, partNumber, partPaths.Count);
                                var partHash = await ComputeSha256Async(partPaths[index]);
                                queueItem.PartDescription = $"Parte {partNumber}/{partPaths.Count}";
                                UploadModalStatus.Text = $"Enviando parte {partNumber}/{partPaths.Count}: {partName}";
                                queueItem.ProgressLabel = $"Enviando parte {partNumber}/{partPaths.Count}";
                                await UploadTelegramMediaAsync(partPaths[index], partName, BuildTelegramCaption(partName, partHash),
                                    destination, GetMimeType(uploadName), ReportProgress);
                                uploadedParts++;
                                SaveIntegrityRecord(splitUuid, partNumber, partName, destinationName, partHash);
                            }
                        }
                        finally
                        {
                            foreach (var partPath in partPaths)
                                try { File.Delete(partPath); } catch { }
                        }
                    }
                    else
                    {
                        queueItem.ProgressLabel = entry.VerifyHash ? "Calculando SHA-256" : "Enviando arquivo";
                        UploadModalStatus.Text = queueItem.ProgressLabel;
                        var localHash = entry.VerifyHash ? await ComputeSha256Async(sourcePath) : null;
                        await UploadTelegramMediaAsync(sourcePath, uploadName, BuildTelegramCaption(uploadName, localHash),
                            destination, GetMimeType(uploadName), ReportProgress);
                        uploadedParts++;
                        if (entry.VerifyHash)
                        {
                            var finalHash = await ComputeSha256Async(sourcePath);
                            if (!string.Equals(localHash, finalHash, StringComparison.OrdinalIgnoreCase))
                                throw new IOException("A verificação local do hash falhou.");
                            SaveIntegrityRecord(Guid.NewGuid().ToString(), 1, uploadName, destinationName, localHash!);
                        }
                    }
                    queueItem.Percentage = 100;
                    queueItem.ProgressLabel = "Concluído";
                    queueItem.Status = "Concluído";
                    queueItem.CanRetry = false;
                    queueItem.ProgressLabel = "Concluído";
                    queueItem.PartDescription = "Concluído";
                    queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - transferStarted);
                    UpdateTransferQueueDisplay();
                    if (ReferenceEquals(_selectedTransfer, queueItem)) RefreshTransferDetails(queueItem);
                    UploadModalStatus.Text = $"Upload concluído: {uploadName}";
                }
                catch (Exception ex)
                {
                    uploadFailed = true;
                    var explanation = ExplainTransferError(ex, "enviar", entry.Credential);
                    queueItem.Status = "Falha";
                    queueItem.ProgressLabel = explanation;
                    queueItem.PartDescription = $"Falha: {queueItem.PartDescription}";
                    queueItem.CanRetry = false;
                    queueItem.Elapsed = FormatElapsed(DateTime.UtcNow - transferStarted);
                    UpdateTransferQueueDisplay();
                    if (ReferenceEquals(_selectedTransfer, queueItem)) RefreshTransferDetails(queueItem);
                    var partialNote = uploadedParts > 0
                        ? $" A operação ficou parcial: {uploadedParts} parte(s) já foram enviadas; verifique o destino antes de repetir."
                        : "";
                    UploadModalStatus.Text = $"Falha ao enviar “{entry.UploadName}”: {explanation}{partialNote}";
                    TelegramFilesStatus.Text = UploadModalStatus.Text;
                    if (explanation.Contains("autenticação", StringComparison.OrdinalIgnoreCase))
                        MarkTelegramDisconnected(explanation);
                    break;
                }
                finally
                {
                    if (tempPath is not null)
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    if (encryptedTempPath is not null)
                    {
                        try { File.Delete(encryptedTempPath); } catch { }
                    }
                }
            }
            if (!uploadFailed)
            {
                await LoadTelegramFilesAsync();
                UploadModalStatus.Text = "Upload concluído.";
                UploadModalOverlay.IsVisible = false;
                _pendingUploads.Clear();
            }
        }
        catch (Exception ex)
        {
            var explanation = ExplainTransferError(ex, "concluir o upload", "Telegram");
            UploadModalStatus.Text = $"Não foi possível concluir o upload: {explanation}";
            TelegramFilesStatus.Text = UploadModalStatus.Text;
            if (explanation.Contains("autenticação", StringComparison.OrdinalIgnoreCase))
                MarkTelegramDisconnected(explanation);
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }

    private long GetEffectiveUploadPartSizeBytes(string connectionName)
    {
        const long conservativeTelegramLimit = 1900L * 1024 * 1024;
        if (_uploadLimits.TryGetValue(connectionName, out var configured)
            && long.TryParse(configured, out var megabytes) && megabytes > 0)
            return megabytes * 1024L * 1024L;
        return conservativeTelegramLimit;
    }

    private static string BuildSplitPartName(string originalName, string uuid, int partNumber, int totalParts)
    {
        var extension = Path.GetExtension(originalName);
        var stem = extension.Length > 0 ? originalName[..^extension.Length] : originalName;
        return $"{stem}_OD[{uuid}_{partNumber}-{totalParts}]{extension}";
    }

    private static async Task<List<string>> SplitFileIntoTemporaryPartsAsync(string sourcePath, long partSize, string uuid)
    {
        var paths = new List<string>();
        await using var input = File.OpenRead(sourcePath);
        var buffer = new byte[1024 * 1024];
        try
        {
            var partNumber = 0;
            while (input.Position < input.Length)
            {
                partNumber++;
                var path = Path.Combine(Path.GetTempPath(), $"OmniDrive_{uuid}_{partNumber}.part");
                paths.Add(path);
                await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                var remaining = Math.Min(partSize, input.Length - input.Position);
                while (remaining > 0)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
                    if (read == 0) throw new EndOfStreamException("O arquivo de origem mudou durante a divisão.");
                    await output.WriteAsync(buffer.AsMemory(0, read));
                    remaining -= read;
                }
            }
            return paths;
        }
        catch
        {
            foreach (var path in paths) try { File.Delete(path); } catch { }
            throw;
        }
    }

    private async Task UploadTelegramMediaAsync(string sourcePath, string telegramFileName, string caption,
        InputPeer destination, string mimeType, Client.ProgressCallback progress)
    {
        if (_telegramClient is null) throw new InvalidOperationException("O cliente do Telegram não está disponível.");
        var uploadedFile = await _telegramClient.UploadFileAsync(sourcePath, progress);
        await _telegramClient.Messages_SendMedia(destination, new InputMediaUploadedDocument
        {
            file = uploadedFile,
            mime_type = mimeType,
            attributes = new TL.DocumentAttribute[] { new DocumentAttributeFilename { file_name = telegramFileName } }
        }, caption, Random.Shared.NextInt64());
    }

    private sealed record IntegrityRecord(string Uuid, int PartNumber, string FileName, string Destination, string Sha256, DateTimeOffset SavedAt);

    private void SaveIntegrityRecord(string uuid, int partNumber, string fileName, string destination, string hash)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var databasePath = Path.Combine(directory, "integrity.json");
            var records = File.Exists(databasePath)
                ? JsonSerializer.Deserialize<List<IntegrityRecord>>(File.ReadAllText(databasePath)) ?? new List<IntegrityRecord>()
                : new List<IntegrityRecord>();
            records.RemoveAll(record => record.Uuid == uuid && record.PartNumber == partNumber);
            records.Add(new IntegrityRecord(uuid, partNumber, fileName, destination, hash, DateTimeOffset.UtcNow));
            File.WriteAllText(databasePath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = $"O arquivo foi enviado, mas não foi possível registrar o hash localmente: {ex.Message}";
        }
    }

    private async Task DownloadPartWithIntegrityCheckAsync(Document document, string destinationPath, string? expectedHash, int partNumber, string displayName, bool? verifyOverride = null, Action<long>? progress = null)
    {
        if (_telegramClient is null) throw new InvalidOperationException("O cliente do Telegram não está disponível.");
        var verify = (verifyOverride ?? (VerifyDownloadHash.IsChecked == true)) && !string.IsNullOrWhiteSpace(expectedHash);

        for (var attempt = 1; attempt <= (verify ? 2 : 1); attempt++)
        {
            await using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var downloadTask = _telegramClient.DownloadFileAsync(document, output);
                while (!downloadTask.IsCompleted)
                {
                    progress?.Invoke(output.Length);
                    await Task.WhenAny(downloadTask, Task.Delay(250));
                }
                await downloadTask;
                progress?.Invoke(output.Length);
            }
            if (!verify) return;

            var actualHash = await ComputeSha256Async(destinationPath);
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase)) return;
            try { File.Delete(destinationPath); } catch { }
            if (attempt == 1)
                TelegramFilesStatus.Text = $"Hash divergente na parte {partNumber} de “{displayName}”. Baixando novamente somente essa parte...";
        }
        throw new InvalidDataException($"A parte {partNumber} de “{displayName}” falhou na verificação SHA-256 após uma nova tentativa.");
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string GetMimeType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
        ".zip" => "application/zip", ".txt" => "text/plain", ".json" => "application/json",
        ".mp4" => "video/mp4", ".mp3" => "audio/mpeg", _ => "application/octet-stream"
    };

    private async Task RefreshUploadDestinationsAsync()
    {
        var destinations = new List<UploadDestination>
        {
            new UploadDestination("Mensagens salvas", new InputPeerSelf(), "self")
        };

        if (_telegramClient?.User is not null)
        {
            try
            {
                var dialogs = await _telegramClient.Messages_GetAllDialogs();
                foreach (var dialog in dialogs.dialogs)
                {
                    var entity = dialogs.UserOrChat(dialog);
                    if (entity is not (TL.Chat or Channel))
                        continue;

                    var chatName = entity switch
                    {
                        TL.Chat chat => chat.title,
                        Channel channel => channel.title,
                        _ => string.Empty
                    };
                    if (!chatName.EndsWith(TelegramFolderMarker, StringComparison.Ordinal))
                        continue;

                    var key = GetTelegramPeerKey(entity);
                    InputPeer? peer = entity switch
                    {
                        TL.Chat chat => new InputPeerChat(chat.id),
                        Channel channel => new InputPeerChannel(channel.id, channel.access_hash),
                        _ => null
                    };
                    if (key is null || peer is null || destinations.Any(item => item.Key == key))
                        continue;

                    var displayName = chatName[..^TelegramFolderMarker.Length];
                    destinations.Add(new UploadDestination(displayName, peer, key));
                }
            }
            catch
            {
                // A falha ao carregar as pastas não deve impedir o envio para Mensagens salvas.
            }
        }

        foreach (var folder in _folderPath)
        {
            if (!destinations.Any(item => item.Key == folder.Key))
                destinations.Add(new UploadDestination(folder.Name, folder.Peer, folder.Key));
        }

        _uploadDestinations = destinations;
    }

    private List<UploadDestination> GetDestinationsForCredential(string credential) =>
        string.Equals(credential, "Telegram", StringComparison.OrdinalIgnoreCase)
            ? _uploadDestinations
            : new List<UploadDestination>();

    private UploadDestination GetCurrentUploadDestination()
    {
        var currentFolder = _folderPath.LastOrDefault();
        if (currentFolder is not null)
        {
            var knownFolder = _uploadDestinations.FirstOrDefault(destination => destination.Key == currentFolder.Key);
            if (knownFolder is not null)
                return knownFolder;

            return new UploadDestination(currentFolder.Name, currentFolder.Peer, currentFolder.Key);
        }

        return _uploadDestinations.FirstOrDefault(destination => destination.Key == "self")
            ?? new UploadDestination("Mensagens salvas", new InputPeerSelf(), "self");
    }

    private sealed class UploadDestination
    {
        public UploadDestination(string name, InputPeer peer, string key)
        {
            Name = name;
            Peer = peer;
            Key = key;
        }

        public string Name { get; }
        public InputPeer Peer { get; }
        public string Key { get; }
        public override string ToString() => Name;
    }

    private sealed class UploadEntry
    {
        public string SourcePath { get; init; } = string.Empty;
        public string OriginalName { get; init; } = string.Empty;
        public string UploadName { get; set; } = string.Empty;
        public string Credential { get; set; } = "Telegram";
        public UploadDestination? Destination { get; set; }
        public bool VerifyHash { get; set; }
        public bool Compress { get; set; }
        public bool Encrypt { get; set; }
        public string EncryptionPassword { get; set; } = string.Empty;
        public bool IsFolder { get; init; }
    }

    private void ShowProviderRequired(string operation) =>
        StatusText.Text = $"Para {operation}, conecte primeiro um serviço de armazenamento.";

    private void SetBreadcrumbRoot(string rootName)
    {
        _folderPath.Clear();
        BreadcrumbCurrent.Content = rootName;
        UpdateBreadcrumb();
    }

    private void BreadcrumbCurrent_Click(object? sender, RoutedEventArgs e)
    {
        if (_folderPath.Count == 0)
            StatusText.Text = $"Você está em {BreadcrumbCurrent.Content}.";
    }

    private async void BreadcrumbPrevious_Click(object? sender, RoutedEventArgs e)
    {
        if (_folderPath.Count > 0)
        {
            _folderPath.RemoveAt(_folderPath.Count - 1);
            UpdateBreadcrumb();
            await LoadTelegramFilesAsync();
        }
    }

    private void UpdateBreadcrumb()
    {
        var current = _folderPath.LastOrDefault();
        BreadcrumbCurrent.Content = current?.Name ?? "Drive na nuvem";
        BreadcrumbPrevious.Content = _folderPath.Count > 1
            ? _folderPath[^2].Name
            : "Drive na nuvem";
        BreadcrumbPrevious.IsVisible = _folderPath.Count > 0;
        BreadcrumbSeparator.IsVisible = _folderPath.Count > 0;
    }

    private async void NavigateToFolder(TelegramFileItem folder)
    {
        if (!folder.IsFolder || string.IsNullOrWhiteSpace(folder.FolderKey))
            return;

        _folderPath.Add(new TelegramFolderLocation(folder.Name, folder.Peer, folder.FolderKey));
        UpdateBreadcrumb();
        await LoadTelegramFilesAsync();
    }

    private async Task CreateTelegramFolderAsync()
    {
        if (_telegramClient?.User is null)
        {
            TelegramFilesStatus.Text = "Conecte uma conta do Telegram antes de criar uma pasta.";
            return;
        }

        var accountName = _telegramClient.User.ToString();
        var nameInput = new TextBox { Watermark = "Nome da pasta", MinWidth = 330 };
        var accountText = new TextBlock
        {
            Text = $"Serviço: Telegram\nConta: {accountName}\nDestino: chat privado do Telegram",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var hint = new TextBlock
        {
            Text = $"O chat será identificado como Nome{TelegramFolderMarker}. O marcador é necessário para o OmniDrive reconhecer a pasta.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A49CAF"))
        };
        var createButton = new Button { Content = "Criar pasta", Classes = { "action" }, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancelar", Classes = { "ghost" }, HorizontalAlignment = HorizontalAlignment.Right };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(createButton);
        var body = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = "Criar pasta no Telegram", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold });
        body.Children.Add(accountText);
        body.Children.Add(nameInput);
        body.Children.Add(hint);
        body.Children.Add(buttons);

        var dialog = new Window
        {
            Title = "Criar pasta · OmniDrive",
            Width = 460,
            Height = 330,
            MinWidth = 460,
            MinHeight = 330,
            MaxWidth = 460,
            MaxHeight = 330,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#201D28")),
            Content = body
        };
        createButton.Click += (_, _) => dialog.Close(nameInput.Text?.Trim());
        cancelButton.Click += (_, _) => dialog.Close(null);

        var requestedName = await dialog.ShowDialog<string?>(this);
        var folderName = requestedName?.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
            return;
        if (folderName.Contains('\n') || folderName.Contains('\r'))
        {
            TelegramFilesStatus.Text = "O nome da pasta deve ficar em uma única linha.";
            return;
        }
        if (folderName.EndsWith(TelegramFolderMarker, StringComparison.Ordinal))
            folderName = folderName[..^TelegramFolderMarker.Length].Trim();
        if (string.IsNullOrWhiteSpace(folderName))
        {
            TelegramFilesStatus.Text = "Informe um nome válido para a pasta.";
            return;
        }

        var title = folderName + TelegramFolderMarker;
        try
        {
            TelegramFilesStatus.Text = $"Criando “{folderName}” no Telegram...";
            await _telegramClient.Channels_CreateChannel(title, "Pasta gerenciada pelo OmniDrive.", megagroup: true);

            var dialogs = await _telegramClient.Messages_GetAllDialogs();
            string? createdKey = null;
            foreach (var item in dialogs.dialogs)
            {
                var entity = dialogs.UserOrChat(item);
                var entityTitle = entity switch
                {
                    TL.Chat chat => chat.title,
                    Channel channel => channel.title,
                    _ => null
                };
                if (!string.Equals(entityTitle, title, StringComparison.Ordinal))
                    continue;
                createdKey = GetTelegramPeerKey(entity!);
                if (createdKey is not null)
                    break;
            }

            if (createdKey is null)
            {
                TelegramFilesStatus.Text = $"O Telegram criou o chat “{title}”, mas ele ainda não apareceu na lista. Clique em Atualizar arquivos.";
                await LoadTelegramFilesAsync();
                return;
            }

            _folderParents[createdKey] = _folderPath.LastOrDefault()?.Key;
            SaveTelegramFolderMap();
            await LoadTelegramFilesAsync();
            TelegramFilesStatus.Text = $"Pasta “{folderName}” criada na conta Telegram {accountName}.";
        }
        catch (Exception ex)
        {
            TelegramFilesStatus.Text = $"Não foi possível criar a pasta no Telegram: {ex.Message}";
        }
    }

    private sealed record TelegramFolderLocation(string Name, InputPeer Peer, string Key);

    private void Search_TextChanged(object? sender, TextChangedEventArgs e) => ApplyCurrentFilters();
    private void ToggleFilters_Click(object? sender, RoutedEventArgs e) => FilterModalOverlay.IsVisible = true;
    private void CloseFilters_Click(object? sender, RoutedEventArgs e) => FilterModalOverlay.IsVisible = false;
    private void FilterModalOverlay_PointerPressed(object? sender, PointerPressedEventArgs e) => FilterModalOverlay.IsVisible = false;
    private void FilterModalCard_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void ApplyFilters_Click(object? sender, RoutedEventArgs e)
    {
        if (!ValidateNumericFilters()) return;
        FilterModalOverlay.IsVisible = false;
        ApplyCurrentFilters();
    }

    private bool ValidateNumericFilters()
    {
        if (!string.IsNullOrWhiteSpace(SizeMinFilter.Text) && (!long.TryParse(SizeMinFilter.Text, out var minSize) || minSize < 0))
        { TelegramFilesStatus.Text = "Informe um tamanho mínimo válido em bytes."; return false; }
        if (!string.IsNullOrWhiteSpace(SizeMaxFilter.Text) && (!long.TryParse(SizeMaxFilter.Text, out var maxSize) || maxSize < 0))
        { TelegramFilesStatus.Text = "Informe um tamanho máximo válido em bytes."; return false; }
        if (long.TryParse(SizeMinFilter.Text, out var sizeMin) && long.TryParse(SizeMaxFilter.Text, out var sizeMax) && sizeMin > sizeMax)
        { TelegramFilesStatus.Text = "O tamanho mínimo não pode ser maior que o máximo."; return false; }
        if (!string.IsNullOrWhiteSpace(PartsCountFilter.Text) && (!int.TryParse(PartsCountFilter.Text, out var partCount) || partCount < 1))
        { TelegramFilesStatus.Text = "O número de partes deve ser um inteiro maior que zero."; return false; }
        if (DateFromFilter.SelectedDate is not null && DateToFilter.SelectedDate is not null && DateFromFilter.SelectedDate.Value.Date > DateToFilter.SelectedDate.Value.Date)
        { TelegramFilesStatus.Text = "A data inicial não pode ser posterior à data final."; return false; }
        return true;
    }

    private int GetActiveFilterCount()
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) count++;
        if (!string.IsNullOrWhiteSpace(NameFilter.Text)) count++;
        if (!string.IsNullOrWhiteSpace(SizeMinFilter.Text) || !string.IsNullOrWhiteSpace(SizeMaxFilter.Text)) count++;
        if (DateFromFilter.SelectedDate is not null || DateToFilter.SelectedDate is not null) count++;
        if (!string.IsNullOrWhiteSpace(ExtensionFilter.Text)) count++;
        if (ServiceFilter.SelectedIndex > 0) count++;
        if (CredentialFilter.SelectedIndex > 0) count++;
        if (FavoritesFilter.SelectedIndex > 0) count++;
        if (TypeFilter.SelectedIndex > 0) count++;
        if (HashFilter.SelectedIndex > 0) count++;
        if (!string.IsNullOrWhiteSpace(PartsCountFilter.Text)) count++;
        return count;
    }

    private void ApplyCurrentFilters()
    {
        if (SearchBox is null || _loadedCloudFiles is null) return;
        var search = SearchBox.Text?.Trim();
        var nameTerms = (NameFilter.Text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hasSizeMin = long.TryParse(SizeMinFilter.Text, out var sizeMin);
        var hasSizeMax = long.TryParse(SizeMaxFilter.Text, out var sizeMax);
        var hasExactPartCount = int.TryParse(PartsCountFilter.Text, out var exactPartCount);
        var extensions = (ExtensionFilter.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith('.') ? value : "." + value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool PassRule(bool active, bool matches, ToggleSwitch mode) => !active || (mode.IsChecked == false ? !matches : matches);

        var filtered = _loadedCloudFiles.Where(file =>
        {
            if (!string.IsNullOrWhiteSpace(search) && !file.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
            var nameActive = nameTerms.Length > 0;
            var nameMatches = nameActive && nameTerms.Any(term => file.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (!PassRule(nameActive, nameMatches, NameRuleMode)) return false;
            var sizeActive = hasSizeMin || hasSizeMax;
            if (!PassRule(sizeActive, (!hasSizeMin || file.SizeBytes >= sizeMin) && (!hasSizeMax || file.SizeBytes <= sizeMax), SizeRuleMode)) return false;
            var dateActive = DateFromFilter.SelectedDate is not null || DateToFilter.SelectedDate is not null;
            var itemDate = file.MessageDate.Date;
            var dateMatches = (DateFromFilter.SelectedDate is null || itemDate >= DateFromFilter.SelectedDate.Value.Date)
                && (DateToFilter.SelectedDate is null || itemDate <= DateToFilter.SelectedDate.Value.Date);
            if (!PassRule(dateActive, dateMatches, DateRuleMode)) return false;
            if (!PassRule(extensions.Count > 0, extensions.Contains(Path.GetExtension(file.DisplayName)), ExtensionRuleMode)) return false;
            if (!PassRule(ServiceFilter.SelectedIndex > 0, ServiceFilter.SelectedIndex == 1, ServiceRuleMode)) return false;
            if (!PassRule(CredentialFilter.SelectedIndex > 0, CredentialFilter.SelectedIndex == 1 && _telegramClient?.User is not null, CredentialRuleMode)) return false;
            var favoriteMatch = FavoritesFilter.SelectedIndex switch { 1 => file.IsFavorite, 2 => !file.IsFavorite, _ => true };
            if (!PassRule(FavoritesFilter.SelectedIndex > 0, favoriteMatch, FavoritesRuleMode)) return false;
            var typeMatch = TypeFilter.SelectedIndex switch { 1 => !file.IsFolder, 2 => file.IsFolder, _ => true };
            if (!PassRule(TypeFilter.SelectedIndex > 0, typeMatch, TypeRuleMode)) return false;
            var hashMatch = HashFilter.SelectedIndex switch { 1 => file.HasSavedHash, 2 => !file.HasSavedHash, _ => true };
            if (!PassRule(HashFilter.SelectedIndex > 0, hashMatch, HashRuleMode)) return false;
            var partCount = file.IsSplitFile ? file.ExpectedPartCount : 1;
            if (!PassRule(hasExactPartCount, partCount == exactPartCount, PartsRuleMode)) return false;
            return true;
        }).ToList();

        CloudFiles.Clear();
        foreach (var file in filtered) CloudFiles.Add(file);
        var activeCount = GetActiveFilterCount();
        FilterCount.Text = activeCount == 0 ? "" : $"({activeCount})";
        EmptyFilesState.IsVisible = CloudFiles.Count == 0;
        CloudFilesContainer.IsVisible = CloudFiles.Count > 0;
        if (CloudFiles.Count == 0)
        {
            FilesEmptyTitle.Text = _loadedCloudFiles.Count == 0 ? "Nenhum arquivo ou pasta encontrado" : "Nenhum item corresponde aos filtros";
            FilesEmptyDescription.Text = _loadedCloudFiles.Count == 0
                ? "Conecte um serviço e clique em Atualizar para carregar arquivos."
                : "Altere os critérios ou clique em Limpar filtros para voltar a ver todos os itens carregados.";
        }
        if (activeCount > 0) TelegramFilesStatus.Text = $"{CloudFiles.Count} de {_loadedCloudFiles.Count} item(ns) correspondem aos filtros.";
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        NameFilter.Text = "";
        SizeMinFilter.Text = ""; SizeMaxFilter.Text = "";
        DateFromFilter.SelectedDate = null; DateToFilter.SelectedDate = null;
        ExtensionFilter.Text = "";
        ServiceFilter.SelectedIndex = 0; CredentialFilter.SelectedIndex = 0;
        FavoritesFilter.SelectedIndex = 0; TypeFilter.SelectedIndex = 0; HashFilter.SelectedIndex = 0;
        PartsCountFilter.Text = "";
        NameRuleMode.IsChecked = true; SizeRuleMode.IsChecked = true; DateRuleMode.IsChecked = true;
        ExtensionRuleMode.IsChecked = true; ServiceRuleMode.IsChecked = true; CredentialRuleMode.IsChecked = true;
        FavoritesRuleMode.IsChecked = true; TypeRuleMode.IsChecked = true; HashRuleMode.IsChecked = true; PartsRuleMode.IsChecked = true;
        ApplyCurrentFilters();
        TelegramFilesStatus.Text = "Filtros limpos.";
    }

    private void AddConnection_Click(object? sender, RoutedEventArgs e)
    {
        var name = NewConnectionName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SettingsStatus.Text = "Digite um nome para a conexão antes de adicionar.";
            return;
        }

        var service = SelectedComboText(NewConnectionService);
        var displayName = $"{service} — {name}";
        if (_connections.Contains(displayName, StringComparer.OrdinalIgnoreCase))
        {
            SettingsStatus.Text = "Já existe uma conexão com esse serviço e nome.";
            return;
        }

        _connections.Add(displayName);
        NewConnectionName.Text = "";
        RefreshConnectionControls(displayName);
        SaveSettings();
        SettingsStatus.Text = $"Conexão “{name}” cadastrada localmente. A autenticação do provedor ainda não está disponível.";
    }

    private void EditConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (ConnectionsList.SelectedItem is not string selected)
        {
            SettingsStatus.Text = "Selecione uma conexão para editar.";
            return;
        }

        var name = NewConnectionName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SettingsStatus.Text = "Digite o novo nome no campo acima e selecione o serviço desejado.";
            return;
        }

        var updated = $"{SelectedComboText(NewConnectionService)} — {name}";
        if (!string.Equals(updated, selected, StringComparison.OrdinalIgnoreCase) &&
            _connections.Contains(updated, StringComparer.OrdinalIgnoreCase))
        {
            SettingsStatus.Text = "Já existe uma conexão com esse serviço e nome.";
            return;
        }

        var index = _connections.IndexOf(selected);
        if (index < 0)
            return;

        _connections[index] = updated;
        if (_uploadLimits.Remove(selected, out var limit))
            _uploadLimits[updated] = limit;

        RefreshConnectionControls(updated);
        SaveSettings();
        SettingsStatus.Text = "Conexão atualizada.";
    }

    private void DeleteConnection_Click(object? sender, RoutedEventArgs e)
    {
        if (ConnectionsList.SelectedItem is not string selected)
        {
            SettingsStatus.Text = "Selecione uma conexão para excluir.";
            return;
        }

        _connections.Remove(selected);
        _uploadLimits.Remove(selected);
        RefreshConnectionControls();
        SaveSettings();
        SettingsStatus.Text = "Conexão removida das preferências locais.";
    }

    private void ConnectionsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ConnectionsList.SelectedItem is string selected)
        {
            var separator = selected.IndexOf(" — ", StringComparison.Ordinal);
            if (separator > 0)
            {
                var service = selected[..separator];
                NewConnectionService.SelectedIndex = service == "Telegram" ? 0 : 1;
                NewConnectionName.Text = selected[(separator + 3)..];
            }

            UploadLimitConnection.SelectedItem = selected;
            UploadLimitValue.Text = _uploadLimits.TryGetValue(selected, out var limit) ? limit : "0";
        }
    }

    private void UploadLimitConnection_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (UploadLimitConnection.SelectedItem is string selected)
            UploadLimitValue.Text = _uploadLimits.TryGetValue(selected, out var limit) ? limit : "0";
    }

    private void SaveUploadLimit_Click(object? sender, RoutedEventArgs e)
    {
        if (UploadLimitConnection.SelectedItem is not string selected)
        {
            SettingsStatus.Text = "Adicione e selecione uma conexão antes de definir um limite.";
            return;
        }

        if (!long.TryParse(UploadLimitValue.Text, out var limit) || limit < 0)
        {
            SettingsStatus.Text = "Informe um limite válido em MB. Use 0 para não definir limite adicional.";
            return;
        }

        _uploadLimits[selected] = limit.ToString();
        SaveSettings();
        SettingsStatus.Text = limit == 0
            ? "Limite salvo: não haverá limite adicional de upload definido pelo usuário. Os limites do serviço continuam valendo."
            : $"Limite de upload salvo para {selected}: {limit} MB. Os limites do serviço continuam valendo.";
    }

    private void SaveTransferSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxUploadsValue.Text, out var uploads) || uploads < 1 ||
            !int.TryParse(MaxDownloadsValue.Text, out var downloads) || downloads < 1)
        {
            SettingsStatus.Text = "Informe números inteiros maiores que zero para uploads e downloads simultâneos.";
            return;
        }

        SaveSettings();
        SettingsStatus.Text = $"Preferências salvas: até {uploads} uploads e {downloads} downloads simultâneos. A fila depende da integração do mecanismo de transferências.";
    }

    private void IntegrityOption_Changed(object? sender, RoutedEventArgs e) =>
        UpdateIntegrityWarnings();

    private void UpdateIntegrityWarnings()
    {
        // Avalonia may raise Checked/Unchecked while loading XAML, before all named
        // controls have been created. Guard both the options and their warnings.
        if (UploadIntegrityWarning is not null && VerifyUploadHash is not null)
            UploadIntegrityWarning.IsVisible = VerifyUploadHash.IsChecked != true;
        if (DownloadIntegrityWarning is not null && VerifyDownloadHash is not null)
            DownloadIntegrityWarning.IsVisible = VerifyDownloadHash.IsChecked != true;
    }

    private void SaveIntegritySettings_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
        SettingsStatus.Text = "Preferências de verificação de hash salvas localmente. A verificação efetiva será usada quando o mecanismo de transferências for integrado.";
    }

    private void RefreshConnectionControls(string? select = null)
    {
        ConnectionsList.Items.Clear();
        UploadLimitConnection.Items.Clear();

        foreach (var connection in _connections)
        {
            ConnectionsList.Items.Add(connection);
            UploadLimitConnection.Items.Add(connection);
        }

        if (select is not null)
        {
            ConnectionsList.SelectedItem = select;
            UploadLimitConnection.SelectedItem = select;
        }
        else if (_connections.Count > 0)
        {
            ConnectionsList.SelectedIndex = 0;
            UploadLimitConnection.SelectedIndex = 0;
        }
    }

    private static string SelectedComboText(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Content?.ToString() ?? "Outro serviço compatível";
        return "Outro serviço compatível";
    }

    private void UpdateTelegramConnectionSummary()
    {
        var credentials = TelegramCredentialStore.Load();
        if (credentials is null)
        {
            TelegramConnectionSummary.Text = "Nenhuma credencial salva";
            TelegramCardConnectButton.Content = "＋ Conectar";
            return;
        }

        var name = string.IsNullOrWhiteSpace(credentials.Name) ? "Telegram" : credentials.Name;
        var account = _telegramClient?.User is not null
            ? $"Conectado como {_telegramClient.User}"
            : "Credencial salva · reconexão automática ao iniciar";
        TelegramConnectionSummary.Text = $"{name} · {account}";
        TelegramCardConnectButton.Content = "＋ Adicionar/Conectar";
    }

    private void EditTelegramCredential_Click(object? sender, RoutedEventArgs e)
    {
        var credentials = TelegramCredentialStore.Load();
        if (credentials is null)
        {
            SettingsStatus.Text = "Ainda não existe uma credencial do Telegram salva. Clique em Conectar para cadastrar uma.";
            ShowTelegramConnect_Click(sender, e);
            return;
        }

        TelegramConnectionName.Text = credentials.Name;
        TelegramApiId.Text = credentials.ApiId;
        TelegramApiHash.Text = credentials.ApiHash;
        TelegramPhone.Text = credentials.PhoneNumber;
        _editingTelegramCredential = true;
        TelegramConnectButton.Content = "Salvar alterações e conectar";
        TelegramSetupOverlay.IsVisible = true;
        TelegramStatus.Text = "Edite os dados desta credencial. As alterações serão salvas apenas para a conta do Telegram configurada aqui.";
    }

    private void ShowTelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        _editingTelegramCredential = false;
        TelegramConnectButton.Content = "Salvar e conectar";
        TelegramSetupOverlay.IsVisible = true;
        TelegramApiId?.Focus();
        SettingsStatus.Text = "Informe as credenciais da aplicação e o número internacional da conta.";
    }

    private void HideTelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        TelegramSetupOverlay.IsVisible = false;
    }

    private void TelegramSetupOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == TelegramSetupOverlay)
            TelegramSetupOverlay.IsVisible = false;
    }

    private void TelegramSetupCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void LoadTelegramCredentials()
    {
        var credentials = TelegramCredentialStore.Load();
        if (credentials is null)
            return;

        TelegramApiId.Text = credentials.ApiId;
        TelegramApiHash.Text = credentials.ApiHash;
        TelegramPhone.Text = credentials.PhoneNumber;
        TelegramConnectionName.Text = string.IsNullOrWhiteSpace(credentials.Name) ? "Telegram" : credentials.Name;
        _hasSavedTelegramCredentials = true;
        UpdateTelegramConnectionSummary();
        TelegramStatus.Text = "Credenciais recuperadas. O OmniDrive tentará conectar automaticamente ao iniciar.";
    }

    private async void TelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        if (_telegramClient?.User is not null && !_editingTelegramCredential)
        {
            TelegramStatus.Text = $"Já conectado ao Telegram como {_telegramClient.User}.";
            return;
        }

        if (_telegramClient?.User is not null && _editingTelegramCredential)
        {
            var editedName = TelegramConnectionName.Text?.Trim();
            var editedApiIdText = TelegramApiId.Text?.Trim();
            var editedApiHash = TelegramApiHash.Text?.Trim();
            var editedPhone = TelegramPhone.Text?.Trim() ?? "";
            var editedPhoneDigits = new string(editedPhone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(editedName) || !int.TryParse(editedApiIdText, out var editedApiId) || editedApiId <= 0 ||
                string.IsNullOrWhiteSpace(editedApiHash) || editedPhoneDigits.Length == 0)
            {
                TelegramStatus.Text = "Informe o nome, API ID, API Hash e telefone para salvar a credencial.";
                return;
            }

            TelegramCredentialStore.Save(new TelegramCredentials(editedApiId.ToString(), editedApiHash, "+" + editedPhoneDigits, editedName));
            _telegramClient.Dispose();
            _telegramClient = null;
            _telegramPendingKind = null;
            _editingTelegramCredential = false;
            TelegramConnectButton.Content = "Salvar e conectar";
            UpdateTelegramConnectionSummary();
        }

        TelegramConnectButton.IsEnabled = false;
        try
        {
            string loginInfo;
            if (_telegramClient is null)
            {
                if (!int.TryParse(TelegramApiId.Text?.Trim(), out var apiId) || apiId <= 0)
                {
                    TelegramStatus.Text = "Informe um API ID numérico válido.";
                    return;
                }

                var apiHash = TelegramApiHash.Text?.Trim();
                var phoneInput = TelegramPhone.Text?.Trim() ?? "";
                var phoneDigits = new string(phoneInput.Where(char.IsDigit).ToArray());
                var phone = phoneDigits.Length > 0 ? "+" + phoneDigits : "";
                if (string.IsNullOrWhiteSpace(apiHash) || string.IsNullOrWhiteSpace(phone))
                {
                    TelegramStatus.Text = "Informe o API Hash e o número de telefone com código do país.";
                    return;
                }

                var connectionName = TelegramConnectionName.Text?.Trim();
                if (string.IsNullOrWhiteSpace(connectionName))
                {
                    TelegramStatus.Text = "Dê um nome à credencial antes de salvar e conectar.";
                    TelegramConnectionName.Focus();
                    return;
                }

                var credentials = new TelegramCredentials(apiId.ToString(), apiHash, phone, connectionName);
                TelegramCredentialStore.Save(credentials);
                _hasSavedTelegramCredentials = true;
                UpdateTelegramConnectionSummary();
                _telegramClient = new Client(apiId, apiHash, TelegramCredentialStore.SessionPath);
                _telegramPendingKind = "phone_number";
                loginInfo = phone;
                TelegramStatus.Text = "Conectando ao Telegram...";
            }
            else
            {
                if (string.IsNullOrEmpty(_telegramPendingKind))
                {
                    TelegramStatus.Text = "A conexão já foi iniciada. Verifique a etapa de autenticação solicitada.";
                    return;
                }

                var resendRequested = _telegramResendRequested;
                loginInfo = _telegramPendingKind switch
                {
                    "phone_number" => "+" + new string((TelegramPhone.Text ?? "").Where(char.IsDigit).ToArray()),
                    "verification_code" when _telegramResendRequested => "",
                    _ => TelegramLoginInput.Text?.Trim() ?? ""
                };
                _telegramResendRequested = false;

                if (string.IsNullOrWhiteSpace(loginInfo) && !resendRequested)
                {
                    TelegramStatus.Text = "Preencha a informação solicitada pelo Telegram para continuar.";
                    return;
                }
            }

            var requested = await _telegramClient.Login(loginInfo);
            if (_telegramClient.User is not null)
            {
                _telegramPendingKind = null;
                TelegramLoginInputPanel.IsVisible = false;
                TelegramLoginInput.Text = "";
                TelegramConnectButton.Content = "Conectado";
                TelegramStatus.Text = $"Conectado ao Telegram como {_telegramClient.User}. A sessão está pronta para consultar seus arquivos.";
                TelegramSetupOverlay.IsVisible = false;
                UpdateTelegramConnectionSummary();
                await LoadTelegramFilesAsync();
                return;
            }

            _telegramPendingKind = requested;
            switch (requested)
            {
                case "verification_code":
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = "Código de verificação enviado pelo Telegram";
                    TelegramLoginInput.Watermark = "Digite o código recebido";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    this.FindControl<Button>("TelegramResendCodeButton")!.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar código";
                    TelegramStatus.Text = "O código nem sempre chega por SMS: confira as mensagens de serviço do Telegram em qualquer sessão já conectada. Se não encontrar, use “Não recebi o código” para solicitar uma alternativa, quando disponível.";
                    break;
                case "password":
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = "Senha de verificação em duas etapas (2FA)";
                    TelegramLoginInput.Watermark = "Senha da conta Telegram";
                    TelegramLoginInput.PasswordChar = '●';
                    this.FindControl<Button>("TelegramResendCodeButton")!.IsVisible = false;
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar senha";
                    TelegramStatus.Text = "Sua conta exige a senha de verificação em duas etapas.";
                    break;
                case "name":
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = "Nome completo";
                    TelegramLoginInput.Watermark = "Nome e sobrenome";
                    TelegramLoginInput.PasswordChar = '\0';
                    this.FindControl<Button>("TelegramResendCodeButton")!.IsVisible = false;
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Continuar";
                    TelegramStatus.Text = "O Telegram solicitou um nome para concluir o cadastro da conta.";
                    break;
                case "email":
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = "E-mail";
                    TelegramLoginInput.Watermark = "Seu e-mail";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Continuar";
                    TelegramStatus.Text = "O Telegram solicitou um endereço de e-mail para a verificação.";
                    break;
                case "email_verification_code":
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = "Código enviado por e-mail";
                    TelegramLoginInput.Watermark = "Código de verificação do e-mail";
                    TelegramLoginInput.PasswordChar = '\0';
                    this.FindControl<Button>("TelegramResendCodeButton")!.IsVisible = false;
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar código";
                    TelegramStatus.Text = "Digite o código enviado para o e-mail solicitado pelo Telegram.";
                    break;
                case null:
                    TelegramStatus.Text = "O Telegram ainda não confirmou a conexão. Tente novamente.";
                    break;
                default:
                    TelegramSetupOverlay.IsVisible = true;
                    TelegramLoginLabel.Text = $"Informação solicitada: {requested}";
                    TelegramLoginInput.Watermark = "Digite a informação solicitada";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Continuar";
                    TelegramStatus.Text = $"O Telegram solicitou uma etapa adicional: {requested}.";
                    break;
            }
        }
        catch (Exception ex)
        {
            TelegramSetupOverlay.IsVisible = true;
            TelegramStatus.Text = $"O Telegram não concluiu a solicitação: {ex.Message}";
            if (_telegramPendingKind == "phone_number")
            {
                _telegramClient?.Dispose();
                _telegramClient = null;
                _telegramPendingKind = null;
                TelegramStatus.Text += " Confira o API ID, API Hash, telefone internacional e conexão com a internet antes de tentar novamente.";
            }
        }
        finally
        {
            TelegramConnectButton.IsEnabled = true;
        }
    }

    private void TelegramResendCode_Click(object? sender, RoutedEventArgs e)
    {
        if (_telegramClient is null || _telegramPendingKind != "verification_code")
        {
            TelegramStatus.Text = "Solicite primeiro o código de login.";
            return;
        }

        _telegramResendRequested = true;
        TelegramLoginInput.Text = "";
        TelegramStatus.Text = "Solicitando ao Telegram uma forma alternativa de entrega do código...";
        TelegramConnect_Click(sender, e);
    }

    private void TelegramForget_Click(object? sender, RoutedEventArgs e)
    {
        _telegramClient?.Dispose();
        _telegramClient = null;
        _telegramPendingKind = null;
        _hasSavedTelegramCredentials = false;
        TelegramCredentialStore.ClearCredentials();
        TelegramCredentialStore.ClearSession();
        TelegramConnectionName.Text = "";
        TelegramApiId.Text = "";
        TelegramApiHash.Text = "";
        TelegramPhone.Text = "";
        TelegramLoginInput.Text = "";
        TelegramLoginInputPanel.IsVisible = false;
        this.FindControl<Button>("TelegramResendCodeButton")!.IsVisible = false;
        _telegramResendRequested = false;
        TelegramConnectButton.Content = "Salvar e conectar";
        UpdateTelegramConnectionSummary();
        TelegramStatus.Text = "Credenciais e sessão local removidas. Para revogar a sessão no Telegram, acesse Configurações > Dispositivos no aplicativo Telegram.";
    }


    private sealed class AppPasswordRecord
    {
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public int Iterations { get; set; } = 600_000;
    }

    private AppPasswordRecord? LoadAppPasswordRecord()
    {
        try
        {
            if (!File.Exists(_appPasswordPath)) return null;
            var record = JsonSerializer.Deserialize<AppPasswordRecord>(File.ReadAllText(_appPasswordPath));
            if (record is null || string.IsNullOrWhiteSpace(record.Salt) || string.IsNullOrWhiteSpace(record.Hash))
                return null;
            return record;
        }
        catch { return null; }
    }

    private static AppPasswordRecord CreateAppPasswordRecord(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
        return new AppPasswordRecord { Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(hash), Iterations = 600_000 };
    }

    private bool VerifyAppPassword(string password)
    {
        var record = LoadAppPasswordRecord();
        if (record is null) return false;
        try
        {
            var salt = Convert.FromBase64String(record.Salt);
            var expected = Convert.FromBase64String(record.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, record.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    private void SaveAppPasswordRecord(string password)
    {
        var directory = Path.GetDirectoryName(_appPasswordPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = _appPasswordPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(CreateAppPasswordRecord(password), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, _appPasswordPath, overwrite: true);
    }

    private void RefreshAppPasswordSettings()
    {
        var status = this.FindControl<TextBlock>("AppPasswordSettingsStatus");
        var button = this.FindControl<Button>("ManageAppPasswordButton");
        if (status is null || button is null) return;
        status.Text = _appPasswordConfigured
            ? "A senha de abertura está ativada. Você pode alterá-la ou removê-la."
            : "Não há senha de abertura configurada. O aplicativo abre diretamente.";
        button.Content = _appPasswordConfigured ? "Alterar ou remover senha" : "Definir senha de abertura";
    }

    private void ShowAppPasswordUnlock()
    {
        _appLocked = true;
        _appPasswordDialogMode = "unlock";
        var overlay = this.FindControl<Grid>("AppPasswordOverlay")!;
        this.FindControl<TextBlock>("AppPasswordDialogTitle")!.Text = "Desbloquear OmniDrive";
        this.FindControl<TextBlock>("AppPasswordDialogDescription")!.Text = "Digite sua senha para abrir o aplicativo.";
        this.FindControl<StackPanel>("AppPasswordCurrentPanel")!.IsVisible = true;
        this.FindControl<StackPanel>("AppPasswordNewPanel")!.IsVisible = false;
        this.FindControl<Button>("RemoveAppPasswordButton")!.IsVisible = false;
        this.FindControl<Button>("CancelAppPasswordButton")!.IsVisible = false;
        this.FindControl<Button>("SaveAppPasswordButton")!.Content = "Desbloquear";
        this.FindControl<TextBox>("AppPasswordCurrent")!.Text = "";
        this.FindControl<TextBox>("AppPasswordNew")!.Text = "";
        this.FindControl<TextBox>("AppPasswordConfirm")!.Text = "";
        this.FindControl<TextBlock>("AppPasswordDialogStatus")!.Text = "";
        overlay.IsVisible = true;
        this.FindControl<TextBox>("AppPasswordCurrent")!.Focus();
    }

    private void ContinueStartupAfterUnlock()
    {
        _appLocked = false;
        if (_hasSavedTelegramCredentials)
            TelegramConnect_Click(this, new RoutedEventArgs());
    }

    private void ManageAppPassword_Click(object? sender, RoutedEventArgs e)
    {
        _appPasswordDialogMode = "manage";
        this.FindControl<TextBlock>("AppPasswordDialogTitle")!.Text = _appPasswordConfigured ? "Alterar senha de abertura" : "Definir senha de abertura";
        this.FindControl<TextBlock>("AppPasswordDialogDescription")!.Text = _appPasswordConfigured
            ? "Informe a senha atual e depois escolha a nova senha. Você também pode remover a proteção."
            : "Crie uma senha com pelo menos 8 caracteres para exigir autenticação ao abrir o OmniDrive.";
        this.FindControl<StackPanel>("AppPasswordCurrentPanel")!.IsVisible = _appPasswordConfigured;
        this.FindControl<StackPanel>("AppPasswordNewPanel")!.IsVisible = true;
        this.FindControl<Button>("RemoveAppPasswordButton")!.IsVisible = _appPasswordConfigured;
        this.FindControl<Button>("CancelAppPasswordButton")!.IsVisible = true;
        this.FindControl<Button>("SaveAppPasswordButton")!.Content = "Salvar senha";
        this.FindControl<TextBox>("AppPasswordCurrent")!.Text = "";
        this.FindControl<TextBox>("AppPasswordNew")!.Text = "";
        this.FindControl<TextBox>("AppPasswordConfirm")!.Text = "";
        this.FindControl<TextBlock>("AppPasswordDialogStatus")!.Text = "";
        this.FindControl<Grid>("AppPasswordOverlay")!.IsVisible = true;
    }

    private void SaveAppPassword_Click(object? sender, RoutedEventArgs e)
    {
        var current = this.FindControl<TextBox>("AppPasswordCurrent")!.Text ?? "";
        var status = this.FindControl<TextBlock>("AppPasswordDialogStatus")!;
        if (_appPasswordDialogMode == "unlock")
        {
            if (!VerifyAppPassword(current))
            {
                status.Text = "Senha incorreta. Tente novamente.";
                this.FindControl<TextBox>("AppPasswordCurrent")!.Text = "";
                return;
            }
            this.FindControl<Grid>("AppPasswordOverlay")!.IsVisible = false;
            ContinueStartupAfterUnlock();
            return;
        }

        if (_appPasswordConfigured && !VerifyAppPassword(current))
        {
            status.Text = "A senha atual está incorreta.";
            return;
        }
        var password = this.FindControl<TextBox>("AppPasswordNew")!.Text ?? "";
        var confirm = this.FindControl<TextBox>("AppPasswordConfirm")!.Text ?? "";
        if (password.Length < 8)
        {
            status.Text = "A nova senha deve ter pelo menos 8 caracteres.";
            return;
        }
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            status.Text = "As senhas não coincidem.";
            return;
        }

        try
        {
            SaveAppPasswordRecord(password);
            _appPasswordConfigured = true;
            RefreshAppPasswordSettings();
            this.FindControl<Grid>("AppPasswordOverlay")!.IsVisible = false;
            this.FindControl<TextBlock>("SettingsStatus")!.Text = "Senha de abertura salva.";
        }
        catch (Exception ex)
        {
            status.Text = $"Não foi possível salvar a senha: {ex.Message}";
        }
    }

    private void RemoveAppPassword_Click(object? sender, RoutedEventArgs e)
    {
        var current = this.FindControl<TextBox>("AppPasswordCurrent")!.Text ?? "";
        var status = this.FindControl<TextBlock>("AppPasswordDialogStatus")!;
        if (!_appPasswordConfigured || !VerifyAppPassword(current))
        {
            status.Text = "Informe a senha atual correta para remover a proteção.";
            return;
        }
        try
        {
            if (File.Exists(_appPasswordPath)) File.Delete(_appPasswordPath);
            _appPasswordConfigured = false;
            RefreshAppPasswordSettings();
            this.FindControl<Grid>("AppPasswordOverlay")!.IsVisible = false;
            this.FindControl<TextBlock>("SettingsStatus")!.Text = "Senha de abertura removida.";
        }
        catch (Exception ex)
        {
            status.Text = $"Não foi possível remover a senha: {ex.Message}";
        }
    }

    private void CancelAppPassword_Click(object? sender, RoutedEventArgs e)
    {
        if (_appPasswordDialogMode == "unlock" || _appLocked)
            return;
        this.FindControl<Grid>("AppPasswordOverlay")!.IsVisible = false;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var data = JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(_settingsPath));
            if (data is null)
                return;

            _connections.Clear();
            _connections.AddRange(data.Connections ?? new List<string>());
            _uploadLimits.Clear();
            foreach (var pair in data.UploadLimits ?? new Dictionary<string, string>())
                _uploadLimits[pair.Key] = pair.Value;

            if (MaxUploadsValue is not null)
                MaxUploadsValue.Text = Math.Max(1, data.MaxUploads).ToString();
            if (MaxDownloadsValue is not null)
                MaxDownloadsValue.Text = Math.Max(1, data.MaxDownloads).ToString();
            if (VerifyUploadHash is not null)
                VerifyUploadHash.IsChecked = data.VerifyUploadHash;
            if (VerifyDownloadHash is not null)
                VerifyDownloadHash.IsChecked = data.VerifyDownloadHash;
            _defaultDownloadFolder = data.DefaultDownloadFolder;
            if (DefaultDownloadFolder is not null)
                DefaultDownloadFolder.Text = _defaultDownloadFolder ?? "";
        }
        catch (Exception ex)
        {
            // A bad settings file must not prevent the main window from opening.
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory ?? ".", "startup.log"),
                    $"[{DateTimeOffset.Now:O}] Failed to load settings: {ex}{Environment.NewLine}");
            }
            catch
            {
                // Logging is best-effort; startup must continue.
            }

            if (SettingsStatus is not null)
                SettingsStatus.Text = "Não foi possível carregar algumas preferências locais; os valores padrão serão usados.";
        }
    }

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var data = new LocalSettings
            {
                Connections = _connections.ToList(),
                UploadLimits = new Dictionary<string, string>(_uploadLimits),
                MaxUploads = int.TryParse(MaxUploadsValue.Text, out var uploads) && uploads > 0 ? uploads : 4,
                MaxDownloads = int.TryParse(MaxDownloadsValue.Text, out var downloads) && downloads > 0 ? downloads : 4,
                VerifyUploadHash = VerifyUploadHash.IsChecked == true,
                VerifyDownloadHash = VerifyDownloadHash.IsChecked == true,
                DefaultDownloadFolder = _defaultDownloadFolder
            };

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            SettingsStatus.Text = "Não foi possível salvar as preferências. Verifique as permissões de gravação local.";
        }
    }

    private sealed class LocalSettings
    {
        public List<string>? Connections { get; set; } = new();
        public Dictionary<string, string>? UploadLimits { get; set; } = new();
        public int MaxUploads { get; set; } = 4;
        public int MaxDownloads { get; set; } = 4;
        public bool VerifyUploadHash { get; set; } = true;
        public bool VerifyDownloadHash { get; set; } = true;
        public string? DefaultDownloadFolder { get; set; }
    }

    private void ToggleTransferQueue_Click(object? sender, RoutedEventArgs e)
    {
        TransferQueuePanel.IsVisible = !TransferQueuePanel.IsVisible;
    }

    private void UpdateTransferQueueDisplay()
    {
        if (TransferQueueCount is not null)
        {
            var completed = TransferQueue.Count(item => item.Status == "Concluído");
            TransferQueueCount.Text = $"{completed} / {TransferQueue.Count}";
        }
        if (TransferQueueEmpty is not null)
            TransferQueueEmpty.IsVisible = TransferQueue.Count == 0;
    }

    private async void RetryTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TransferQueueItem item } && item.CanRetry && item.RetryAction is not null)
        {
            item.CanRetry = false;
            await item.RetryAction();
        }
    }

    private void RemoveTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TransferQueueItem item })
        {
            if (item.Status == "Aguardando")
            {
                item.Status = "Cancelado";
                item.ProgressLabel = "Cancelado antes de iniciar";
            }
            else if (item.Status == "Em andamento")
            {
                item.ProgressLabel = "A transferência está em andamento e não pode ser interrompida com segurança nesta versão.";
                if (ReferenceEquals(_selectedTransfer, item)) RefreshTransferDetails(item);
                return;
            }
            TransferQueue.Remove(item);
            if (ReferenceEquals(_selectedTransfer, item))
                TransferDetailsOverlay.IsVisible = false;
            UpdateTransferQueueDisplay();
        }
    }

    private void TransferItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TransferItemsList.SelectedItem is not TransferQueueItem item)
            return;

        _selectedTransfer = item;
        RefreshTransferDetails(item);
        TransferDetailsOverlay.IsVisible = true;
    }

    private void RefreshTransferDetails(TransferQueueItem item)
    {
        TransferDetailName.Text = item.FileName;
        TransferDetailService.Text = $"Serviço: {item.Service}";
        TransferDetailCredential.Text = $"Credencial: {item.Credential}";
        TransferDetailPercent.Text = $"Progresso: {item.Percentage:0.##}% · {item.Status}";
        TransferDetailSpeed.Text = $"Velocidade: {item.Speed}";
        TransferDetailProgress.Value = item.Percentage;
        TransferDetailElapsed.Text = item.Elapsed;
        TransferDetailSize.Text = item.Size;
        TransferDetailPart.Text = item.PartDescription == "—" ? "Parte: arquivo não dividido" : $"Parte atual: {item.PartDescription}";
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} s" : $"{Math.Max(0, (int)elapsed.TotalSeconds)} s";

    private void TransferDetailsOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        TransferDetailsOverlay.IsVisible = false;
        _selectedTransfer = null;
    }

    private void TransferDetailsCard_PointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void CloseTransferDetails_Click(object? sender, RoutedEventArgs e)
    {
        TransferDetailsOverlay.IsVisible = false;
        _selectedTransfer = null;
    }


    private void ToggleDetails_Click(object? sender, RoutedEventArgs e)
    {
        if (DetailsPanel.Parent is not Grid parentGrid)
            return;

        _detailsVisible = !_detailsVisible;
        DetailsPanel.IsVisible = _detailsVisible;
        ShowDetailsButton.IsVisible = !_detailsVisible && DriveContent.IsVisible;
        parentGrid.ColumnDefinitions[1].Width = _detailsVisible
            ? new GridLength(300)
            : new GridLength(0);
        UpdateTransferQueuePosition();
    }

    private void UpdateTransferQueuePosition()
    {
        TransferQueueHost.Margin = _detailsVisible && DriveContent.IsVisible
            ? new Thickness(0, 0, 322, 18)
            : new Thickness(0, 0, 22, 18);
    }

    private sealed class RelayUiCommand(System.Action action) : System.Windows.Input.ICommand
    {
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}