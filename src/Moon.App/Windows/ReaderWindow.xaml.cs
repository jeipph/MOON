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
using Moon.Core.Annotations;
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
        BuildSelectionBar();
        _viewModel.Annotations.CollectionChanged += (_, _) => UpdateNotesHint();
        UpdateNotesHint();
        // La barra de colores es una ventanita aparte: se esconde si la ventana se mueve o se va.
        LocationChanged += (_, _) => HideSelectionBar();
        Deactivated += (_, _) => SelectionPopup.IsOpen = false;
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
                _viewModel.OnReady(Text(data, "title"), toc, Text(data, "language"));
                Page.Focus();
                break;
            case "tts-block":
                var sentences = data.TryGetProperty("items", out JsonElement list)
                    ? list.EnumerateArray().Select(i => (Text(i, "mark"), Text(i, "text") ?? "")).ToList()
                    : [];
                _viewModel.OnTtsBlock(sentences, Text(data, "chapter") ?? "");
                break;
            case "tts-end":
                _viewModel.OnTtsEnd(Text(data, "error"));
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
            case "selection":
                ShowSelectionBar(data);
                break;
            case "selection-cleared":
                HideSelectionBar();
                break;
            case "annotation-clicked":
                if (Text(data, "cfi") is { } cfi && _viewModel.FindByLocation(cfi) is { } clicked)
                    EditNote(clicked);
                break;
        }
    }

    // ---- Subrayar ----

    // Barra que aparece debajo del texto seleccionado: cinco colores, "Nota" y "Copiar".
    private void BuildSelectionBar()
    {
        foreach (HighlightColor color in Enum.GetValues<HighlightColor>())
        {
            var button = new Button
            {
                Style = (Style)FindResource("IconButton"),
                ToolTip = $"Subrayar en {NoteDialog.ColorName(color).ToLowerInvariant()}",
                Content = new System.Windows.Shapes.Ellipse
                {
                    Width = (double)FindResource("Size.ColorDot"),
                    Height = (double)FindResource("Size.ColorDot"),
                    Fill = (Brush)FindResource($"Brush.Highlight.{color}"),
                },
            };
            System.Windows.Automation.AutomationProperties.SetName(button, $"Subrayar {NoteDialog.ColorName(color)}");
            button.Click += (_, _) => { _viewModel.HighlightSelection(color); HideSelectionBar(); };
            SelectionBar.Children.Add(button);
        }
        var note = new Button { Style = (Style)FindResource("ChipButton"), Content = "Nota…", Margin = (Thickness)FindResource("Space.Gap") };
        note.Click += (_, _) =>
        {
            // Una nota siempre va sobre un subrayado (amarillo si no se elige otro).
            HideSelectionBar();
            if (_viewModel.HighlightSelection(HighlightColor.Yellow) is { } item)
                EditNote(item);
        };
        var copy = new Button { Style = (Style)FindResource("ChipButton"), Content = "Copiar", Margin = (Thickness)FindResource("Space.Gap") };
        copy.Click += (_, _) =>
        {
            if (_viewModel.Selection is { } s)
                Clipboard.SetText(s.Text);
            HideSelectionBar();
            Send(new { type = "clear-selection" });
        };
        SelectionBar.Children.Add(note);
        SelectionBar.Children.Add(copy);
    }

    private void ShowSelectionBar(JsonElement data)
    {
        string? cfi = Text(data, "cfi");
        string? text = Text(data, "text");
        if (cfi is null || string.IsNullOrWhiteSpace(text))
            return;
        double Number(string name) => data.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        _viewModel.Selection = new ReaderViewModel.PageSelection(cfi, text, Text(data, "chapter") ?? "", Number("fraction"),
            Text(data, "paragraph") ?? "", (int)Number("offset"));

        SelectionPopup.IsOpen = false; // para que recalcule la posición
        SelectionBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = SelectionBar.DesiredSize.Width;
        // Con una palabra (o tres como mucho) se muestra también la traducción: la barra es más alta.
        bool mayShowDictionary = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3;
        double height = mayShowDictionary ? 150 : 48;
        double below = Number("y") + 8;
        // Si no cabe debajo del texto, va encima.
        double y = below + height > Page.ActualHeight ? Number("top") - height - 8 : below;
        SelectionPopup.HorizontalOffset = Math.Clamp(Number("x") - width / 2, 0, Math.Max(0, Page.ActualWidth - width));
        SelectionPopup.VerticalOffset = Math.Max(0, y);
        SelectionPopup.IsOpen = true;
    }

    private void HideSelectionBar()
    {
        SelectionPopup.IsOpen = false;
        _viewModel.Selection = null;
    }

    private void EditNote(AnnotationItemViewModel item)
    {
        var dialog = new NoteDialog(this, item.Text, item.Note, item.Annotation.Color,
            showColors: item.Annotation.Kind != AnnotationKind.Bookmark);
        switch (dialog.Ask())
        {
            case NoteDialogResult.Save:
                _viewModel.UpdateAnnotation(item, dialog.Note, dialog.Color);
                break;
            case NoteDialogResult.Delete:
                _viewModel.DeleteAnnotation(item);
                break;
        }
        Page.Focus();
    }

    // ---- Panel de notas ----

    private void OnNoteClicked(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>()?.DataContext is AnnotationItemViewModel item)
            _viewModel.GoToAnnotationCommand.Execute(item);
    }

    private void OnNoteDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>()?.DataContext is AnnotationItemViewModel item)
            EditNote(item);
    }

    private void OnEditNoteClick(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is AnnotationItemViewModel item)
            EditNote(item);
    }

    private void OnDeleteNoteClick(object sender, RoutedEventArgs e)
    {
        if (NotesList.SelectedItem is AnnotationItemViewModel item)
            _viewModel.DeleteAnnotation(item);
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Annotations.Count == 0)
        {
            MessageBox.Show(this, "Este libro aún no tiene subrayados ni notas.", "MOON");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar notas",
            FileName = string.Concat(_viewModel.Title.Split(Path.GetInvalidFileNameChars())) + " - notas",
            Filter = "Markdown (Obsidian, Notion…)|*.md|CSV (Excel)|*.csv",
        };
        if (dialog.ShowDialog(this) == true)
            _viewModel.Export(dialog.FileName);
    }

    private void UpdateNotesHint() =>
        NotesHint.Visibility = _viewModel.Annotations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
