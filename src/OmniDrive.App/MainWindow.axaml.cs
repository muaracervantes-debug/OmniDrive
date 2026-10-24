using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Input;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using WTelegram;

namespace OmniDrive.App;

public partial class MainWindow : Window
{
    private bool _detailsVisible = true;
    private Client? _telegramClient;
    private string? _telegramPendingKind;
    private readonly List<string> _connections = new();
    private readonly Dictionary<string, string> _uploadLimits = new();
    private int _completedTransfers;
    private TransferQueueItem? _selectedTransfer;
    public ObservableCollection<TransferQueueItem> TransferQueue { get; } = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive",
        "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        TransferItemsList.ItemsSource = TransferQueue;
        UpdateTransferQueueDisplay();
        LoadSettings();
        LoadTelegramCredentials();
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
        FilterPanel = this.FindControl<Border>(nameof(FilterPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FilterPanel");
        NameRuleMode = this.FindControl<ComboBox>(nameof(NameRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: NameRuleMode");
        SizeMinFilter = this.FindControl<TextBox>(nameof(SizeMinFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeMinFilter");
        SizeMaxFilter = this.FindControl<TextBox>(nameof(SizeMaxFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeMaxFilter");
        SizeRuleMode = this.FindControl<ComboBox>(nameof(SizeRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SizeRuleMode");
        DateFromFilter = this.FindControl<DatePicker>(nameof(DateFromFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateFromFilter");
        DateToFilter = this.FindControl<DatePicker>(nameof(DateToFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateToFilter");
        DateRuleMode = this.FindControl<ComboBox>(nameof(DateRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DateRuleMode");
        ExtensionFilter = this.FindControl<TextBox>(nameof(ExtensionFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ExtensionFilter");
        ExtensionRuleMode = this.FindControl<ComboBox>(nameof(ExtensionRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ExtensionRuleMode");
        ServiceFilter = this.FindControl<ComboBox>(nameof(ServiceFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ServiceFilter");
        ServiceRuleMode = this.FindControl<ComboBox>(nameof(ServiceRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: ServiceRuleMode");
        CredentialFilter = this.FindControl<ComboBox>(nameof(CredentialFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CredentialFilter");
        CredentialRuleMode = this.FindControl<ComboBox>(nameof(CredentialRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: CredentialRuleMode");
        FavoritesFilter = this.FindControl<ComboBox>(nameof(FavoritesFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FavoritesFilter");
        FavoritesRuleMode = this.FindControl<ComboBox>(nameof(FavoritesRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: FavoritesRuleMode");
        TypeFilter = this.FindControl<ComboBox>(nameof(TypeFilter)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TypeFilter");
        TypeRuleMode = this.FindControl<ComboBox>(nameof(TypeRuleMode)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TypeRuleMode");
        BackButton = this.FindControl<Button>(nameof(BackButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: BackButton");
        BreadcrumbCurrent = this.FindControl<TextBlock>(nameof(BreadcrumbCurrent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: BreadcrumbCurrent");
        SelectionCount = this.FindControl<TextBlock>(nameof(SelectionCount)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SelectionCount");
        SelectAllCheckBox = this.FindControl<CheckBox>(nameof(SelectAllCheckBox)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SelectAllCheckBox");
        EmptyFilesState = this.FindControl<Grid>(nameof(EmptyFilesState)) ?? throw new InvalidOperationException("Controle XAML não encontrado: EmptyFilesState");
        DetailsPanel = this.FindControl<Border>(nameof(DetailsPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: DetailsPanel");
        SettingsContent = this.FindControl<Grid>(nameof(SettingsContent)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsContent");
        SettingsConnectPage = this.FindControl<ScrollViewer>(nameof(SettingsConnectPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsConnectPage");
        SettingsUploadPage = this.FindControl<ScrollViewer>(nameof(SettingsUploadPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsUploadPage");
        SettingsTransfersPage = this.FindControl<ScrollViewer>(nameof(SettingsTransfersPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsTransfersPage");
        SettingsIntegrityPage = this.FindControl<ScrollViewer>(nameof(SettingsIntegrityPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsIntegrityPage");
        SettingsFolderPage = this.FindControl<ScrollViewer>(nameof(SettingsFolderPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsFolderPage");
        SettingsSecurityPage = this.FindControl<ScrollViewer>(nameof(SettingsSecurityPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsSecurityPage");
        SettingsLocalDataPage = this.FindControl<ScrollViewer>(nameof(SettingsLocalDataPage)) ?? throw new InvalidOperationException("Controle XAML não encontrado: SettingsLocalDataPage");
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
        TelegramApiId = this.FindControl<TextBox>(nameof(TelegramApiId)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramApiId");
        TelegramApiHash = this.FindControl<TextBox>(nameof(TelegramApiHash)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramApiHash");
        TelegramPhone = this.FindControl<TextBox>(nameof(TelegramPhone)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramPhone");
        TelegramLoginInputPanel = this.FindControl<StackPanel>(nameof(TelegramLoginInputPanel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginInputPanel");
        TelegramLoginLabel = this.FindControl<TextBlock>(nameof(TelegramLoginLabel)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginLabel");
        TelegramLoginInput = this.FindControl<TextBox>(nameof(TelegramLoginInput)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramLoginInput");
        TelegramConnectButton = this.FindControl<Button>(nameof(TelegramConnectButton)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramConnectButton");
        TelegramStatus = this.FindControl<TextBlock>(nameof(TelegramStatus)) ?? throw new InvalidOperationException("Controle XAML não encontrado: TelegramStatus");
    }

    private void Overview_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Visão geral";
        StatusText.Text = "Nenhum serviço conectado";
        BreadcrumbCurrent.Text = "Visão geral";
        SetSelectedNavigation(sender);
    }

    private void CloudDrive_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Drive na nuvem";
        StatusText.Text = "Aguardando conexão com um serviço";
        BreadcrumbCurrent.Text = "Raiz";
        SetSelectedNavigation(sender);
    }

    private void Trash_Click(object? sender, RoutedEventArgs e)
    {
        ShowDriveView();
        AreaTitle.Text = "Lixeira";
        StatusText.Text = "A lixeira estará disponível quando um provedor compatível for conectado";
        SetSelectedNavigation(sender);
    }

    private void SettingsSection_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string section)
            return;

        SettingsConnectPage.IsVisible = section == "connect";
        SettingsUploadPage.IsVisible = section == "upload";
        SettingsTransfersPage.IsVisible = section == "transfers";
        SettingsIntegrityPage.IsVisible = section == "integrity";
        SettingsFolderPage.IsVisible = section == "folder";
        SettingsSecurityPage.IsVisible = section == "security";
        SettingsLocalDataPage.IsVisible = section == "local";

        foreach (var item in this.GetVisualDescendants().OfType<Button>())
        {
            if (item.Tag is string tag && new[] { "connect", "upload", "transfers", "integrity", "folder", "security", "local" }.Contains(tag))
            {
                item.Classes.Set("selected", tag == section);
            }
        }
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        DriveContent.IsVisible = false;
        DetailsPanel.IsVisible = false;
        SettingsContent.IsVisible = true;
        SettingsStatus.Text = "Configurações locais. Conexões ainda não autenticam com os provedores.";
        SetSelectedNavigation(sender);
    }

    private void ShowDriveView()
    {
        SettingsContent.IsVisible = false;
        DriveContent.IsVisible = true;
        DetailsPanel.IsVisible = _detailsVisible;
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

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = "Criar pasta",
            Command = new RelayUiCommand(() => ShowProviderRequired("criar uma pasta"))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Enviar arquivo",
            Command = new RelayUiCommand(() => ShowProviderRequired("enviar um arquivo"))
        });
        menu.Items.Add(new MenuItem
        {
            Header = "Enviar pasta",
            Command = new RelayUiCommand(() => ShowProviderRequired("enviar uma pasta"))
        });

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        button.ContextMenu = menu;
        menu.Open(button);
    }

    private void ShowProviderRequired(string operation) =>
        StatusText.Text = $"Para {operation}, conecte primeiro um serviço de armazenamento.";

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        BreadcrumbCurrent.Text = "Raiz";
        BackButton.IsEnabled = false;
        StatusText.Text = "Você já está na raiz. Nenhum serviço conectado.";
    }

    private void Search_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (FilterCount is not null && !string.IsNullOrEmpty(FilterCount.Text))
            return;

        StatusText.Text = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? "Nenhum serviço conectado"
            : "Pesquisa por nome preparada; conecte um serviço para pesquisar arquivos.";
    }

    private void ToggleFilters_Click(object? sender, RoutedEventArgs e) =>
        FilterPanel.IsVisible = !FilterPanel.IsVisible;

    private void ApplyFilters_Click(object? sender, RoutedEventArgs e)
    {
        var activeRules = new List<string>();

        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            activeRules.Add($"{RuleMode(NameRuleMode)} nome");
        if (!string.IsNullOrWhiteSpace(SizeMinFilter.Text) || !string.IsNullOrWhiteSpace(SizeMaxFilter.Text))
            activeRules.Add($"{RuleMode(SizeRuleMode)} tamanho");
        if (DateFromFilter.SelectedDate is not null || DateToFilter.SelectedDate is not null)
            activeRules.Add($"{RuleMode(DateRuleMode)} data");
        if (!string.IsNullOrWhiteSpace(ExtensionFilter.Text))
            activeRules.Add($"{RuleMode(ExtensionRuleMode)} extensão");
        if (ServiceFilter.SelectedIndex > 0)
            activeRules.Add($"{RuleMode(ServiceRuleMode)} serviço");
        if (CredentialFilter.SelectedIndex > 0)
            activeRules.Add($"{RuleMode(CredentialRuleMode)} credencial");
        if (FavoritesFilter.SelectedIndex > 0)
            activeRules.Add($"{RuleMode(FavoritesRuleMode)} favoritos");
        if (TypeFilter.SelectedIndex > 0)
            activeRules.Add($"{RuleMode(TypeRuleMode)} tipo");

        FilterCount.Text = activeRules.Count == 0 ? "" : $"({activeRules.Count})";
        FilterPanel.IsVisible = false;
        StatusText.Text = activeRules.Count == 0
            ? "Filtros limpos. Nenhum serviço conectado."
            : $"Filtros configurados: {string.Join(", ", activeRules)}. Conecte um serviço para aplicá-los aos arquivos.";
    }

    private void ClearFilters_Click(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SizeMinFilter.Text = "";
        SizeMaxFilter.Text = "";
        DateFromFilter.SelectedDate = null;
        DateToFilter.SelectedDate = null;
        ExtensionFilter.Text = "";
        ServiceFilter.SelectedIndex = 0;
        CredentialFilter.SelectedIndex = 0;
        FavoritesFilter.SelectedIndex = 0;
        TypeFilter.SelectedIndex = 0;

        NameRuleMode.SelectedIndex = 0;
        SizeRuleMode.SelectedIndex = 0;
        DateRuleMode.SelectedIndex = 0;
        ExtensionRuleMode.SelectedIndex = 0;
        ServiceRuleMode.SelectedIndex = 0;
        CredentialRuleMode.SelectedIndex = 0;
        FavoritesRuleMode.SelectedIndex = 0;
        TypeRuleMode.SelectedIndex = 0;

        FilterCount.Text = "";
        StatusText.Text = "Filtros limpos. Nenhum serviço conectado.";
    }

    private static string RuleMode(ComboBox selector) =>
        selector.SelectedIndex == 1 ? "Excluir" : "Incluir";

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

    private void ShowTelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        TelegramSetupPanel.IsVisible = true;
        TelegramApiId.Focus();
        SettingsStatus.Text = "Informe as credenciais da aplicação e o número internacional da conta.";
    }

    private void HideTelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        TelegramSetupPanel.IsVisible = false;
    }

    private void LoadTelegramCredentials()
    {
        var credentials = TelegramCredentialStore.Load();
        if (credentials is null)
            return;

        TelegramApiId.Text = credentials.ApiId;
        TelegramApiHash.Text = credentials.ApiHash;
        TelegramPhone.Text = credentials.PhoneNumber;
        TelegramStatus.Text = "Credenciais recuperadas do armazenamento protegido do Windows. Pressione Conectar para restaurar a sessão.";
    }

    private async void TelegramConnect_Click(object? sender, RoutedEventArgs e)
    {
        if (_telegramClient?.User is not null)
        {
            TelegramStatus.Text = $"Já conectado ao Telegram como {_telegramClient.User}.";
            return;
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
                var phone = TelegramPhone.Text?.Trim();
                if (string.IsNullOrWhiteSpace(apiHash) || string.IsNullOrWhiteSpace(phone))
                {
                    TelegramStatus.Text = "Informe o API Hash e o número de telefone com código do país.";
                    return;
                }

                var credentials = new TelegramCredentials(apiId.ToString(), apiHash, phone);
                TelegramCredentialStore.Save(credentials);
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

                loginInfo = _telegramPendingKind switch
                {
                    "phone_number" => TelegramPhone.Text?.Trim() ?? "",
                    _ => TelegramLoginInput.Text?.Trim() ?? ""
                };

                if (string.IsNullOrWhiteSpace(loginInfo))
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
                TelegramStatus.Text = $"Conectado ao Telegram como {_telegramClient.User}. As credenciais e a sessão foram salvas para próximas versões.";
                return;
            }

            _telegramPendingKind = requested;
            switch (requested)
            {
                case "verification_code":
                    TelegramLoginLabel.Text = "Código de verificação enviado pelo Telegram";
                    TelegramLoginInput.Watermark = "Digite o código recebido";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar código";
                    TelegramStatus.Text = "O Telegram solicitou um código de verificação. Insira-o abaixo e pressione Confirmar código.";
                    break;
                case "password":
                    TelegramLoginLabel.Text = "Senha de verificação em duas etapas (2FA)";
                    TelegramLoginInput.Watermark = "Senha da conta Telegram";
                    TelegramLoginInput.PasswordChar = '●';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar senha";
                    TelegramStatus.Text = "Sua conta exige a senha de verificação em duas etapas.";
                    break;
                case "name":
                    TelegramLoginLabel.Text = "Nome completo";
                    TelegramLoginInput.Watermark = "Nome e sobrenome";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Continuar";
                    TelegramStatus.Text = "O Telegram solicitou um nome para concluir o cadastro da conta.";
                    break;
                case "email":
                    TelegramLoginLabel.Text = "E-mail";
                    TelegramLoginInput.Watermark = "Seu e-mail";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Continuar";
                    TelegramStatus.Text = "O Telegram solicitou um endereço de e-mail para a verificação.";
                    break;
                case "email_verification_code":
                    TelegramLoginLabel.Text = "Código enviado por e-mail";
                    TelegramLoginInput.Watermark = "Código de verificação do e-mail";
                    TelegramLoginInput.PasswordChar = '\0';
                    TelegramLoginInputPanel.IsVisible = true;
                    TelegramConnectButton.Content = "Confirmar código";
                    TelegramStatus.Text = "Digite o código enviado para o e-mail solicitado pelo Telegram.";
                    break;
                case null:
                    TelegramStatus.Text = "O Telegram ainda não confirmou a conexão. Tente novamente.";
                    break;
                default:
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
            TelegramStatus.Text = $"Não foi possível concluir a conexão: {ex.Message}";
        }
        finally
        {
            TelegramConnectButton.IsEnabled = true;
        }
    }

    private void TelegramForget_Click(object? sender, RoutedEventArgs e)
    {
        _telegramClient?.Dispose();
        _telegramClient = null;
        _telegramPendingKind = null;
        TelegramCredentialStore.ClearCredentials();
        TelegramCredentialStore.ClearSession();
        TelegramApiId.Text = "";
        TelegramApiHash.Text = "";
        TelegramPhone.Text = "";
        TelegramLoginInput.Text = "";
        TelegramLoginInputPanel.IsVisible = false;
        TelegramConnectButton.Content = "Conectar ao Telegram";
        TelegramStatus.Text = "Credenciais e sessão local removidas. Para revogar a sessão no Telegram, acesse Configurações > Dispositivos no aplicativo Telegram.";
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
                VerifyDownloadHash = VerifyDownloadHash.IsChecked == true
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
    }

    private void ToggleTransferQueue_Click(object? sender, RoutedEventArgs e)
    {
        TransferQueuePanel.IsVisible = !TransferQueuePanel.IsVisible;
    }

    private void UpdateTransferQueueDisplay()
    {
        if (TransferQueueCount is not null)
            TransferQueueCount.Text = $"{_completedTransfers} / {_completedTransfers + TransferQueue.Count}";
        if (TransferQueueEmpty is not null)
            TransferQueueEmpty.IsVisible = TransferQueue.Count == 0;
    }

    private void RemoveTransfer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TransferQueueItem item })
        {
            // The transfer engine will attach its cancellation token here when implemented.
            item.CancelRequested = true;
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
        TransferDetailName.Text = item.FileName;
        TransferDetailService.Text = $"Serviço: {item.Service}";
        TransferDetailCredential.Text = $"Credencial: {item.Credential}";
        TransferDetailPercent.Text = $"Progresso: {item.Percentage:0.##}%";
        TransferDetailSpeed.Text = $"Velocidade: {item.Speed}";
        TransferDetailProgress.Value = item.Percentage;
        TransferDetailElapsed.Text = item.Elapsed;
        TransferDetailSize.Text = item.Size;
        TransferDetailPart.Text = item.PartDescription == "—" ? "Parte: arquivo não dividido" : $"Parte atual: {item.PartDescription}";
        TransferDetailsOverlay.IsVisible = true;
    }

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
        parentGrid.ColumnDefinitions[1].Width = _detailsVisible
            ? new GridLength(300)
            : new GridLength(0);
    }

    private sealed class RelayUiCommand(System.Action action) : System.Windows.Input.ICommand
    {
        public event System.EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}