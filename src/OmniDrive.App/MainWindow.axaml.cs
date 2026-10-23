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
        if (sender is TextBox { Text: { Length: > 0 } })
            StatusText.Text = "A pesquisa será executada quando houver um serviço conectado.";
        else
            StatusText.Text = "Nenhum serviço conectado";
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