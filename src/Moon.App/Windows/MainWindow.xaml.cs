using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.Windows.Shell;

namespace Moon.App.Windows;

/// <summary>Una fila de la cuadrícula de libros (tantos como caben a lo ancho).</summary>
public sealed record BookRow(IReadOnlyList<BookItemViewModel> Books);

public partial class MainWindow : Window
{
    // Windows avisa con WM_DEVICECHANGE al conectar o desconectar un disco.
    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;

    private readonly MainViewModel _viewModel;
    private readonly ThemeService _themes;
    private readonly ObservableCollection<BookRow> _rows = [];
    private int _columns;
    private bool _regroupPending;
    private int _coversReleased;
    private const int CoverReleaseBatch = 40;

    public MainWindow(MainViewModel viewModel, ThemeService themes)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _themes = themes;
        _themes.Changed += (_, _) => UpdateTitleBar();
        SourceInitialized += OnSourceInitialized;
        BookGrid.ItemsSource = _rows;
        _viewModel.Library.Books.CollectionChanged += (_, _) => ScheduleRegroup();
    }

    // ---- Cuadrícula de libros en filas (virtualizada) ----

    // Al filtrar, la lista cambia libro a libro: se reagrupa una sola vez, al terminar.
    private void ScheduleRegroup()
    {
        if (_regroupPending)
            return;
        _regroupPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _regroupPending = false;
            Regroup();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // La rueda baja una cantidad fija de píxeles por "clic": suave y igual con ratón o panel táctil
    // (el panel virtual, por sí solo, a veces no movía la lista con la rueda).
    private void OnBookGridWheel(object sender, MouseWheelEventArgs e)
    {
        if (BookGrid.Template.FindName("Scroller", BookGrid) is not ScrollViewer scroller)
            return;
        scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta / 120.0 * (double)FindResource("Library.WheelPixels"));
        e.Handled = true;
    }

    private void OnBookGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ColumnsFor(e.NewSize.Width) != _columns)
            Regroup();
    }

    private int ColumnsFor(double width)
    {
        double card = (double)FindResource("Size.Card.Width") + ((Thickness)FindResource("Margin.Card")).Right;
        return Math.Max(1, (int)((width - SystemParameters.VerticalScrollBarWidth) / card));
    }

    private void Regroup()
    {
        _columns = ColumnsFor(BookGrid.ActualWidth);
        _rows.Clear();
        foreach (BookItemViewModel[] row in _viewModel.Library.Books.Chunk(_columns))
            _rows.Add(new BookRow(row));
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

    // ---- Teclado ----

    // Ctrl+F busca, Esc borra la búsqueda, F5 vuelve a revisar las carpetas.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (e.Key)
        {
            case Key.F when ctrl:
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;
            case Key.Escape when SearchBox.IsKeyboardFocusWithin && SearchBox.Text.Length > 0:
                _viewModel.Library.SearchText = "";
                break;
            case Key.F5:
                _viewModel.Library.RequestSync();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Desde la búsqueda, Enter o ↓ llevan a la primera tarjeta.
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Down && FirstCard() is { } card)
        {
            card.Focus();
            e.Handled = true;
        }
    }

    private FrameworkElement? FirstCard() => FindCard(BookGrid);

    private static FrameworkElement? FindCard(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement { Name: "Card" } card)
                return card;
            if (FindCard(child) is { } found)
                return found;
        }
        return null;
    }

    // En una tarjeta: Enter abre el libro y las flechas pasan a la de al lado.
    // El menú del libro sale con Mayús+F10 o la tecla de menú.
    private void OnBookKeyDown(object sender, KeyEventArgs e)
    {
        FocusNavigationDirection? direction = e.Key switch
        {
            Key.Left => FocusNavigationDirection.Left,
            Key.Right => FocusNavigationDirection.Right,
            Key.Up => FocusNavigationDirection.Up,
            Key.Down => FocusNavigationDirection.Down,
            _ => null,
        };
        if (e.Key == Key.Enter)
            _viewModel.Library.OpenBookCommand.Execute(BookOf(sender));
        else if (direction is { } d && sender is FrameworkElement card)
        {
            card.MoveFocus(new TraversalRequest(d));
            (Keyboard.FocusedElement as FrameworkElement)?.BringIntoView();
        }
        else
            return;
        e.Handled = true;
    }

    private void OnVocabularyClick(object sender, RoutedEventArgs e) =>
        new VocabularyWindow(this, _viewModel.Library.Service).ShowDialog();

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await _viewModel.Library.DropAsync(paths);
    }

    // Las portadas se leen cuando la tarjeta aparece, no todas al abrir.
    private void OnBookLoaded(object sender, RoutedEventArgs e) => BookOf(sender)?.EnsureCover();

    // ...y se sueltan cuando la fila sale de la pantalla. Las imágenes viven fuera de la memoria de .NET
    // y .NET tarda en notarlo: cada cierto número de portadas soltadas se le pide que limpie.
    private void OnBookUnloaded(object sender, RoutedEventArgs e)
    {
        BookOf(sender)?.ReleaseCover();
        if (++_coversReleased % CoverReleaseBatch == 0)
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
    }

    private void OnBookMouseDown(object sender, MouseButtonEventArgs e)
    {
        (sender as UIElement)?.Focus(); // desde aquí se sigue con las flechas y Enter
        if (e.ClickCount == 2)
            _viewModel.Library.OpenBookCommand.Execute(BookOf(sender));
    }

    private void OnOpenBookClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.OpenBookCommand.Execute(BookOf(sender));

    private void OnOpenPdfInOtherModeClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.OpenPdfInOtherModeCommand.Execute(BookOf(sender));

    private void OnShowInExplorerClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.ShowInExplorerCommand.Execute(BookOf(sender));

    private void OnRemoveBookClick(object sender, RoutedEventArgs e) =>
        _viewModel.Library.RemoveBookCommand.Execute(BookOf(sender));

    private static BookItemViewModel? BookOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as BookItemViewModel;
}
