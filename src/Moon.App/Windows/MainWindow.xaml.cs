using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.Windows.Shell;

namespace Moon.App.Windows;

public partial class MainWindow : Window
{
    // Windows avisa con WM_DEVICECHANGE al conectar o desconectar un disco.
    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;

    private readonly MainViewModel _viewModel;
    private readonly ThemeService _themes;

    public MainWindow(MainViewModel viewModel, ThemeService themes)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _themes = themes;
        _themes.Changed += (_, _) => UpdateTitleBar();
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Trae la ventana al frente (cuando se abre MOON otra vez estando ya abierto).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBar();
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmDeviceChange && (int)wParam is DbtDeviceArrival or DbtDeviceRemoveComplete)
            _viewModel.Library.RequestSync();
        return 0;
    }

    // La barra de título la dibuja Windows: hay que decirle que vaya a juego con el tema.
    private void UpdateTitleBar()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
            TitleBar.SetDark(hwnd, !_themes.IsLight);
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await _viewModel.Library.DropAsync(paths);
    }

    // Las portadas se leen cuando la tarjeta aparece, no todas al abrir.
    private void OnBookLoaded(object sender, RoutedEventArgs e) => BookOf(sender)?.EnsureCover();

    private void OnBookMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            _viewModel.Library.OpenBookCommand.Execute(BookOf(sender));
    }

    private void OnOpenBookClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.OpenBookCommand.Execute(BookOf(sender));

    private void OnShowInExplorerClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.ShowInExplorerCommand.Execute(BookOf(sender));

    private void OnRemoveBookClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.RemoveBookCommand.Execute(BookOf(sender));

    private static BookItemViewModel? BookOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as BookItemViewModel;
}
