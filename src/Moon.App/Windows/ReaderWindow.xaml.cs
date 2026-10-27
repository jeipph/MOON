using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.Core.Models;
using Moon.Windows.Shell;

namespace Moon.App.Windows;

/// <summary>
/// Ventana de lectura. La página es una web local (Web/reader.html + foliate-js) dentro de WebView2,
/// sin acceso a internet: solo ve la carpeta de MOON (app.moon) y la carpeta del libro (book.moon).
/// </summary>
public partial class ReaderWindow : Window
{
    private readonly ReaderViewModel _viewModel;
    private readonly WebViewHost _webViewHost;
    private readonly ThemeService _themes;
    private bool _isFullScreen;
    private WindowState _stateBeforeFullScreen;
    private bool _pickingFromToc;

    public ReaderWindow(ReaderViewModel viewModel, WebViewHost webViewHost, ThemeService themes)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _webViewHost = webViewHost;
        _themes = themes;
        _viewModel.SendToPage = Send;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ReaderViewModel.PageTheme))
                UpdatePageColors();
            else if (e.PropertyName is nameof(ReaderViewModel.Error))
                ShowError();
            else if (e.PropertyName is nameof(ReaderViewModel.CurrentHref))
                SelectCurrentChapter();
        };
        _themes.Changed += OnThemeChanged;
        SourceInitialized += (_, _) => UpdateTitleBar();
        Loaded += async (_, _) => await InitializePageAsync();
        Closed += (_, _) =>
        {
            _themes.Changed -= OnThemeChanged;
            Page.Dispose();
            _viewModel.Dispose();
        };
        UpdatePageColors();
    }

    public long BookId => _viewModel.Book.Id;

    private async Task InitializePageAsync()
    {
        try
        {
            CoreWebView2Environment environment = await _webViewHost.GetEnvironmentAsync();
            await Page.EnsureCoreWebView2Async(environment);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or COMException or InvalidOperationException)
        {
            _viewModel.OnError("falta el componente WebView2 de Windows (Microsoft Edge WebView2 Runtime).");
            return;
        }

        CoreWebView2 web = Page.CoreWebView2;
        web.Settings.AreDefaultContextMenusEnabled = false;
        web.Settings.IsStatusBarEnabled = false;
        web.Settings.IsZoomControlEnabled = false;        // Ctrl + rueda: lo maneja MOON (tamaño de letra)
        web.Settings.AreBrowserAcceleratorKeysEnabled = false; // sin F5, Ctrl+P, Ctrl+F del navegador
        web.Settings.IsGeneralAutofillEnabled = false;
        web.Settings.IsPasswordAutosaveEnabled = false;
#if !DEBUG
        web.Settings.AreDevToolsEnabled = false;
#endif
        web.SetVirtualHostNameToFolderMapping(WebViewHost.AppHost, WebViewHost.WebFolder, CoreWebView2HostResourceAccessKind.Deny);
        // La página de MOON (app.moon) lee el libro desde book.moon: otra "dirección", así que hay que permitirlo.
        web.SetVirtualHostNameToFolderMapping(WebViewHost.BookHost, Path.GetDirectoryName(_viewModel.FilePath)!, CoreWebView2HostResourceAccessKind.Allow);

        // Nada sale de MOON: ni navegar a otras páginas ni abrir ventanas nuevas.
        web.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith($"https://{WebViewHost.AppHost}/", StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        };
        web.NewWindowRequested += (_, e) => e.Handled = true;
        web.WebMessageReceived += OnPageMessage;
        web.Navigate($"https://{WebViewHost.AppHost}/reader.html");
    }

    private void Send(object message)
    {
        if (Page.CoreWebView2 is { } web)
            web.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    // Mensajes de reader.js
    private void OnPageMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument json = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement data = json.RootElement;
        switch (Text(data, "type"))
        {
            case "loaded":
                string url = $"https://{WebViewHost.BookHost}/{Uri.EscapeDataString(Path.GetFileName(_viewModel.FilePath))}";
                Send(_viewModel.OpenMessage(url));
                break;
            case "ready":
                var toc = data.TryGetProperty("toc", out JsonElement items)
                    ? items.EnumerateArray().Select(i => new TocEntry(Text(i, "label") ?? "", Text(i, "href") ?? "", i.GetProperty("depth").GetInt32())).ToList()
                    : [];
                _viewModel.OnReady(Text(data, "title"), toc);
                Page.Focus();
                break;
            case "relocate":
                _viewModel.OnRelocate(
                    data.TryGetProperty("fraction", out JsonElement f) && f.ValueKind == JsonValueKind.Number ? f.GetDouble() : 0,
                    Text(data, "cfi"), Text(data, "chapter") ?? "", Text(data, "href"));
                break;
            case "key":
                HandleKey(Text(data, "key") ?? "", data.TryGetProperty("ctrl", out JsonElement ctrl) && ctrl.GetBoolean());
                break;
            case "external-link":
                OpenExternalLink(Text(data, "href"));
                break;
            case "error":
                _viewModel.OnError(Text(data, "message") ?? "error desconocido");
                break;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Teclas cuando el foco está en la ventana (no en la página).
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        string key = e.Key switch
        {
            Key.Left or Key.PageUp => "ArrowLeft",
            Key.Right or Key.PageDown => "ArrowRight",
            Key.F11 => "F11",
            Key.Escape => "Escape",
            Key.OemPlus or Key.Add => "+",
            Key.OemMinus or Key.Subtract => "-",
            Key.D0 or Key.NumPad0 => "0",
            _ => "",
        };
        if (key.Length == 0 || e.OriginalSource is TextBox)
            return;
        e.Handled = HandleKey(key, ctrl);
    }

    private bool HandleKey(string key, bool ctrl)
    {
        switch (key)
        {
            case "ArrowLeft": _viewModel.PreviousCommand.Execute(null); return true;
            case "ArrowRight": _viewModel.NextCommand.Execute(null); return true;
            case "F11": ToggleFullScreen(); return true;
            case "Escape":
                if (_isFullScreen) ToggleFullScreen();
                else if (SettingsButton.IsChecked == true) SettingsButton.IsChecked = false;
                else if (_viewModel.IsTocOpen) _viewModel.IsTocOpen = false;
                return true;
            case "+" or "=" when ctrl: _viewModel.BiggerTextCommand.Execute(null); return true;
            case "-" when ctrl: _viewModel.SmallerTextCommand.Execute(null); return true;
            case "0" when ctrl: _viewModel.ResetTextCommand.Execute(null); return true;
            default: return false;
        }
    }

    private void OnFullScreenClick(object sender, RoutedEventArgs e) => ToggleFullScreen();

    // Pantalla completa: sin bordes ni barras, solo la página. Esc o F11 para salir.
    private void ToggleFullScreen()
    {
        _isFullScreen = !_isFullScreen;
        if (_isFullScreen)
        {
            _stateBeforeFullScreen = WindowState;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal; // si no, Windows deja visible la barra de tareas
            WindowState = WindowState.Maximized;
            TopRow.Height = new GridLength(0);
            BottomRow.Height = new GridLength(0);
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _stateBeforeFullScreen;
            TopRow.Height = (GridLength)FindResource("Size.TopBar");
            BottomRow.Height = (GridLength)FindResource("Size.ReaderBottom");
        }
        Page.Focus();
    }

    private void OnTocSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_pickingFromToc || TocList.SelectedItem is not TocEntry entry)
            return;
        _viewModel.GoToCommand.Execute(entry);
        Page.Focus();
    }

    // Clic en el capítulo que ya está marcado: la selección no cambia, pero hay que ir a su principio.
    private void OnTocClicked(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>()?.DataContext is TocEntry entry)
        {
            _viewModel.GoToCommand.Execute(entry);
            Page.Focus();
        }
    }

    // Marca en el índice el capítulo que se está leyendo (sin saltar a él).
    private void SelectCurrentChapter()
    {
        TocEntry? current = _viewModel.Toc.FirstOrDefault(t => t.Href == _viewModel.CurrentHref);
        _pickingFromToc = true;
        TocList.SelectedItem = current;
        if (current is not null)
            TocList.ScrollIntoView(current);
        _pickingFromToc = false;
    }

    // Sangría de los subcapítulos.
    private void OnTocItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock { Tag: int depth } text)
            text.Margin = new Thickness(depth * (double)FindResource("Size.TocIndent"), 0, 0, 0);
    }

    private void OnProgressDragCompleted(object sender, DragCompletedEventArgs e) =>
        _viewModel.GoToFraction(ProgressSlider.Value);

    private void OnProgressClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not Thumb)
            _viewModel.GoToFraction(ProgressSlider.Value);
    }

    private void OpenExternalLink(string? href)
    {
        if (href is null || !Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https" or "mailto"))
            return;
        var answer = MessageBox.Show(this, $"Este enlace del libro abre una página de internet en tu navegador:\n\n{uri}\n\n¿Abrirla?",
            "MOON", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateTitleBar();
        UpdatePageColors();
    }

    // La zona alrededor de la página toma el color de la página (sin marco de otro color).
    private void UpdatePageColors()
    {
        string name = _viewModel.EffectivePageTheme switch
        {
            PageTheme.Sepia => "Sepia",
            PageTheme.Dark => "Dark",
            _ => "Light",
        };
        var background = (SolidColorBrush)FindResource($"Brush.Page.{name}");
        var foreground = (SolidColorBrush)FindResource($"Brush.PageText.{name}");
        PageArea.Background = background;
        Page.DefaultBackgroundColor = System.Drawing.Color.FromArgb(background.Color.A, background.Color.R, background.Color.G, background.Color.B);
        foreach (Button arrow in new[] { PreviousButton, NextButton })
        {
            arrow.Background = background;
            arrow.Foreground = foreground;
        }
        ((FrameworkElement)PreviousButton.Parent).SetValue(Panel.BackgroundProperty, background);
    }

    private void ShowError()
    {
        bool hasError = _viewModel.Error is not null;
        ErrorText.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
        Page.Visibility = hasError ? Visibility.Hidden : Visibility.Visible;
    }

    private void UpdateTitleBar()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
            TitleBar.SetDark(hwnd, !_themes.IsLight);
    }
}
