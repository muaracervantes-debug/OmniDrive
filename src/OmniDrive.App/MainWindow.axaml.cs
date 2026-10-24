using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace OmniDrive.App;

public partial class MainWindow : Window
{
    private bool _detailsVisible = true;
    private readonly List<string> _connections = new();
    private readonly Dictionary<string, string> _uploadLimits = new();
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive",
        "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        RefreshConnectionControls();
        UpdateIntegrityWarnings();
    }

    private void InitializeComponent() =>
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

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
        if (UploadIntegrityWarning is not null)
            UploadIntegrityWarning.IsVisible = VerifyUploadHash.IsChecked != true;
        if (DownloadIntegrityWarning is not null)
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

            MaxUploadsValue.Text = Math.Max(1, data.MaxUploads).ToString();
            MaxDownloadsValue.Text = Math.Max(1, data.MaxDownloads).ToString();
            VerifyUploadHash.IsChecked = data.VerifyUploadHash;
            VerifyDownloadHash.IsChecked = data.VerifyDownloadHash;
        }
        catch (Exception)
        {
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