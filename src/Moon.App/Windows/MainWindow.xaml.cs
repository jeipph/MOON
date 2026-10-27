using System.Windows;
using System.Windows.Interop;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.Windows.Shell;

namespace Moon.App.Windows;

public partial class MainWindow : Window
{
    private readonly ThemeService _themes;

    public MainWindow(MainViewModel viewModel, ThemeService themes)
    {
        InitializeComponent();
        DataContext = viewModel;
        _themes = themes;
        _themes.Changed += (_, _) => UpdateTitleBar();
        SourceInitialized += (_, _) => UpdateTitleBar();
    }

    /// <summary>Trae la ventana al frente (cuando se abre MOON otra vez estando ya abierto).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    // La barra de título la dibuja Windows: hay que decirle que vaya a juego con el tema.
    private void UpdateTitleBar()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
            TitleBar.SetDark(hwnd, !_themes.IsLight);
    }
}
