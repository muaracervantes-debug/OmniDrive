using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace OmniDrive.App;

public partial class MainWindow : Window
{
    private bool _detailsVisible = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() =>
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    private void Overview_Click(object? sender, RoutedEventArgs e)
    {
        AreaTitle.Text = "Visão geral";
        StatusText.Text = "Nenhum serviço conectado";
        BreadcrumbCurrent.Text = "Visão geral";
        SetSelectedNavigation(sender);
    }

    private void CloudDrive_Click(object? sender, RoutedEventArgs e)
    {
        AreaTitle.Text = "Drive na nuvem";
        StatusText.Text = "Aguardando conexão com um serviço";
        BreadcrumbCurrent.Text = "Raiz";
        SetSelectedNavigation(sender);
    }

    private void Trash_Click(object? sender, RoutedEventArgs e)
    {
        AreaTitle.Text = "Lixeira";
        StatusText.Text = "A lixeira estará disponível quando um provedor compatível for conectado";
        SetSelectedNavigation(sender);
    }

    private void Settings_Click(object? sender, RoutedEventArgs e)
    {
        AreaTitle.Text = "Configurações";
        StatusText.Text = "Configurações — prévia visual";
        SetSelectedNavigation(sender);
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
        if (FilterCount is not null && FilterCount.Text.Length > 0)
            return;

        StatusText.Text = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? "Nenhum serviço conectado"
            : "Pesquisa por nome preparada; conecte um serviço para pesquisar arquivos.";
    }

    private void ToggleFilters_Click(object? sender, RoutedEventArgs e)
    {
        FilterPanel.IsVisible = !FilterPanel.IsVisible;
    }

    private void ApplyFilters_Click(object? sender, RoutedEventArgs e)
    {
        var activeRules = new List<string>();

        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            activeRules.Add($"{RuleMode(NameRuleMode)} nome");

        if (!string.IsNullOrWhiteSpace(SizeMinFilter.Text) ||
            !string.IsNullOrWhiteSpace(SizeMaxFilter.Text))
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