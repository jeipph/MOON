using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.Core.Annotations;
using Moon.Core.Comics;
using Moon.Core.Models;
using Moon.Windows.Shell;

namespace Moon.App.Windows;

/// <summary>Una página en modo webtoon: tamaño conocido de antemano e imagen que se carga al verse.</summary>
public sealed partial class WebtoonPage(int index) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    public partial double Width { get; set; }

    [ObservableProperty]
    public partial double Height { get; set; }

    [ObservableProperty]
    public partial ImageSource? Image { get; set; }

    private volatile bool _isOnScreen;

    /// <summary>Si la página está en pantalla ahora. Se lee desde el hilo que decodifica.</summary>
    public bool IsOnScreen
    {
        get => _isOnScreen;
        set => _isOnScreen = value;
    }
}

/// <summary>
/// Lector de cómics y manga. Dibuja las páginas con WPF (sin navegador): una, dos o todas
/// en columna (webtoon). Clic a los lados o flechas para pasar; Ctrl + rueda para acercar.
/// </summary>
public partial class ComicReaderWindow : Window
{
    // Fracción del ancho a cada lado que, al hacer clic, pasa la página.
    private const double TurnZone = 0.35;
    // Un clic que se mueve más que esto es un arrastre (para mover una página ampliada).
    private const double DragThreshold = 6;
    // Ancho máximo de la columna en modo webtoon con "Página entera" (como en el móvil).
    private const double WebtoonColumn = 900;
    // Cada cuántas páginas que salen de pantalla (en webtoon) se pide limpiar la memoria.
    private const int ReleaseBatch = 12;

    private readonly ComicReaderViewModel _viewModel;
    private readonly ThemeService _themes;
    private readonly ObservableCollection<WebtoonPage> _webtoonPages = [];
    private readonly DispatcherTimer _resizeTimer;
    private int _renderVersion;
    private bool _isFullScreen;
    private WindowState _stateBeforeFullScreen;
    private Point? _dragStart;
    private Point _scrollStart;
    private bool _dragged;
    private ScrollViewer? _webtoonScroller;
    private bool _scrollingToPage;
    private bool _webtoonPositioning;
    private int _pagesReleased;
    private Grid? _regionView;
    private Point _regionStart;
    private Rectangle? _regionPreview;

    public ComicReaderWindow(ComicReaderViewModel viewModel, ThemeService themes)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _themes = themes;
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeTimer.Tick += (_, _) => { _resizeTimer.Stop(); Render(); };
        WebtoonList.ItemsSource = _webtoonPages;

        _viewModel.ViewChanged += (_, _) => Render();
        _viewModel.RegionsChanged += (_, _) => DrawRegions();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Annotations.CollectionChanged += (_, _) => UpdateNotesHint();
        UpdateNotesHint();
        _themes.Changed += OnThemeChanged;
        SourceInitialized += (_, _) => UpdateTitleBar();
        PageArea.SizeChanged += (_, _) => _resizeTimer.Start();
        Loaded += (_, _) =>
        {
            _viewModel.Pages.MeasureAllInBackground();
            Render();
        };
        Closed += (_, _) =>
        {
            _themes.Changed -= OnThemeChanged;
            _webtoonPages.Clear();
            SpreadPanel.Children.Clear();
            _viewModel.Dispose();
            // Las imágenes de las páginas ocupan memoria fuera de .NET: al cerrar el cómic se devuelve
            // a Windows ya, en vez de esperar a que .NET decida limpiar (podían quedar cientos de MB).
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        };
    }

    public long BookId => _viewModel.Book.Id;

    private double Dpi => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ComicReaderViewModel.Layout))
            Render();
        else if (e.PropertyName == nameof(ComicReaderViewModel.IsNotesOpen))
        {
            // Con el panel abierto, las páginas se colocan en el espacio que queda a su izquierda.
            PageArea.Margin = _viewModel.IsNotesOpen
                ? new Thickness(0, 0, (double)FindResource("Size.SettingsPanel"), 0)
                : new Thickness(0);
            _resizeTimer.Start();
        }
        else if (e.PropertyName == nameof(ComicReaderViewModel.IsMarking))
        {
            PagedScroller.Cursor = _viewModel.IsMarking ? Cursors.Cross : null;
            if (_viewModel.IsMarking && _viewModel.Layout == PageLayout.Webtoon)
                ShowError("Para marcar zonas, cambia la vista a «Una página» o «Doble».");
        }
    }

    private void UpdateNotesHint() =>
        NotesHint.Visibility = _viewModel.Annotations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // ---- Dibujar ----

    private void Render()
    {
        if (!IsLoaded)
            return;
        bool webtoon = _viewModel.Layout == PageLayout.Webtoon;
        PagedScroller.Visibility = webtoon ? Visibility.Collapsed : Visibility.Visible;
        WebtoonList.Visibility = webtoon ? Visibility.Visible : Visibility.Collapsed;
        if (webtoon)
            RenderWebtoon();
        else
            RenderSpread();
    }

    // Una o dos páginas: se calcula el tamaño para que quepan (o llenen el ancho) y se cargan.
    private void RenderSpread()
    {
        int version = ++_renderVersion;
        int[] pages = SpreadLayout.OnScreen(_viewModel.CurrentSpread, _viewModel.Direction);
        double width = Math.Max(100, PageArea.ActualWidth - SystemParameters.VerticalScrollBarWidth);
        double height = Math.Max(100, PageArea.ActualHeight);

        // Con la misma altura, cada página mide ancho = alto ÷ aspecto.
        double widthPerHeight = pages.Sum(p => 1 / _viewModel.Pages.Aspect(p));
        double pageHeight = _viewModel.Fit == ComicFit.Page
            ? Math.Min(height, width / widthPerHeight)
            : width / widthPerHeight;
        pageHeight *= _viewModel.Zoom;

        // Cada página es la imagen con una capa encima para las zonas marcadas.
        while (SpreadPanel.Children.Count < pages.Length)
        {
            var view = new Grid();
            view.Children.Add(new Image { Stretch = Stretch.Fill, SnapsToDevicePixels = true });
            view.Children.Add(new Canvas { ClipToBounds = true });
            SpreadPanel.Children.Add(view);
        }
        while (SpreadPanel.Children.Count > pages.Length)
            SpreadPanel.Children.RemoveAt(SpreadPanel.Children.Count - 1);

        for (int i = 0; i < pages.Length; i++)
        {
            var view = (Grid)SpreadPanel.Children[i];
            var image = (Image)view.Children[0];
            int page = pages[i];
            double pageWidth = pageHeight / _viewModel.Pages.Aspect(page);
            view.Width = image.Width = pageWidth;
            view.Height = image.Height = pageHeight;
            if (image.Tag is not int shown || shown != page)
            {
                image.Source = null; // no dejar la página anterior mientras carga la nueva
                image.Tag = page;
            }
            view.Tag = page;
            LoadInto(image, page, pageWidth, version);
        }
        DrawRegions();

        PagedScroller.ScrollToTop();
        if (_viewModel.IsRightToLeft) PagedScroller.ScrollToRightEnd(); else PagedScroller.ScrollToLeftEnd();
        Prefetch(pageHeight);
    }

    private async void LoadInto(Image image, int page, double displayWidth, int version)
    {
        BitmapSource? bitmap = await _viewModel.Pages.GetAsync(page, (int)(displayWidth * Dpi), _viewModel.Adjustments);
        if (version != _renderVersion || image.Tag is not int current || current != page)
            return; // mientras cargaba, se pasó a otra página
        if (bitmap is null)
        {
            ShowError($"La página {page + 1} está dañada y no se puede mostrar.");
            return;
        }
        image.Source = bitmap;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // Se preparan las páginas de al lado para que pasar sea instantáneo.
    private void Prefetch(double pageHeight)
    {
        int last = _viewModel.CurrentSpread.Max();
        int first = _viewModel.CurrentSpread.Min();
        foreach (int page in new[] { last + 1, last + 2, first - 1, first - 2 })
        {
            if (page >= 0 && page < _viewModel.PageCount)
                _ = _viewModel.Pages.GetAsync(page, (int)(pageHeight / _viewModel.Pages.Aspect(page) * Dpi), _viewModel.Adjustments);
        }
    }

    // Webtoon: todas las páginas en columna con su tamaño; las imágenes se cargan al aparecer.
    private void RenderWebtoon()
    {
        double available = Math.Max(100, PageArea.ActualWidth - SystemParameters.VerticalScrollBarWidth);
        double columnWidth = (_viewModel.Fit == ComicFit.Width ? available : Math.Min(available, WebtoonColumn)) * _viewModel.Zoom;
        bool rebuild = _webtoonPages.Count != _viewModel.PageCount;
        if (rebuild)
        {
            _webtoonPages.Clear();
            for (int i = 0; i < _viewModel.PageCount; i++)
                _webtoonPages.Add(new WebtoonPage(i));
        }
        foreach (WebtoonPage page in _webtoonPages)
        {
            bool resized = Math.Abs(page.Width - columnWidth) > 0.5;
            page.Width = columnWidth;
            page.Height = columnWidth * _viewModel.Pages.Aspect(page.Index);
            if (resized || page.Image is not null)
                page.Image = null; // tamaño o ajustes nuevos: se volverá a cargar al verse
        }
        foreach (WebtoonPage page in _webtoonPages.Where(p => IsRealized(p)))
            LoadWebtoonPage(page);

        // Se recuerda la página ahora: al montar la lista, esta avisa de que "está arriba del todo"
        // y eso no debe cambiar la página en la que se estaba leyendo.
        int target = _viewModel.CurrentPage;
        _webtoonPositioning = true;
        Dispatcher.BeginInvoke(() =>
        {
            WebtoonList.UpdateLayout();
            ScrollWebtoonTo(target);
            _webtoonPositioning = false;
        }, DispatcherPriority.ContextIdle);
    }

    private bool IsRealized(WebtoonPage page) => WebtoonList.ItemContainerGenerator.ContainerFromItem(page) is not null;

    private void OnWebtoonPageLoaded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WebtoonPage page)
        {
            page.IsOnScreen = true;
            LoadWebtoonPage(page);
        }
    }

    // Al salir de la pantalla se suelta la imagen: un webtoon de 300 páginas no llena la memoria.
    private void OnWebtoonPageUnloaded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WebtoonPage page)
        {
            page.IsOnScreen = false;
            page.Image = null;
            // .NET no "ve" lo que pesan las imágenes (viven fuera de su memoria) y tarda en limpiar:
            // cada cierto número de páginas que salen de pantalla se le pide que lo haga.
            if (++_pagesReleased % ReleaseBatch == 0)
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
        }
    }

    private async void LoadWebtoonPage(WebtoonPage page)
    {
        if (page.Image is not null)
            return;
        double width = page.Width;
        // Si se baja rápido, la página puede salir de pantalla antes de decodificarla: entonces se salta.
        BitmapSource? bitmap = await _viewModel.Pages.GetAsync(page.Index, (int)(width * Dpi), _viewModel.Adjustments, () => page.IsOnScreen);
        if (bitmap is not null && Math.Abs(page.Width - width) < 0.5 && page.IsOnScreen)
            page.Image = bitmap;
    }

    private ScrollViewer? WebtoonScroller => _webtoonScroller ??= WebtoonList.FindDescendant<ScrollViewer>();

    private void ScrollWebtoonTo(int pageIndex)
    {
        if (WebtoonScroller is not { } scroller)
            return;
        double offset = _webtoonPages.Take(pageIndex).Sum(p => p.Height);
        _scrollingToPage = true;
        scroller.ScrollToVerticalOffset(offset);
        _scrollingToPage = false;
    }

    // La página "actual" en webtoon es la que ocupa el tercio de arriba de la pantalla.
    private void OnWebtoonScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollingToPage || _webtoonPositioning || _webtoonPages.Count == 0)
            return;
        double target = e.VerticalOffset + e.ViewportHeight / 3;
        double top = 0;
        foreach (WebtoonPage page in _webtoonPages)
        {
            if (target < top + page.Height)
            {
                _viewModel.ReportVisiblePage(page.Index);
                return;
            }
            top += page.Height;
        }
        _viewModel.ReportVisiblePage(_webtoonPages.Count - 1);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    // ---- Teclado ----

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool webtoon = _viewModel.Layout == PageLayout.Webtoon;
        switch (e.Key)
        {
            case Key.Left when !webtoon: _viewModel.GoLeft(); break;
            case Key.Right when !webtoon: _viewModel.GoRight(); break;
            case Key.Space or Key.PageDown: ScreenDown(); break;
            case Key.PageUp: ScreenUp(); break;
            case Key.Home: _viewModel.FirstCommand.Execute(null); if (webtoon) ScrollWebtoonTo(0); break;
            case Key.End: _viewModel.LastCommand.Execute(null); if (webtoon) ScrollWebtoonTo(_viewModel.LastPage); break;
            case Key.F11: ToggleFullScreen(); break;
            case Key.Escape when _isFullScreen: ToggleFullScreen(); break;
            case Key.Escape when ViewButton.IsChecked == true: ViewButton.IsChecked = false; break;
            case Key.OemPlus or Key.Add when ctrl: _viewModel.ZoomInCommand.Execute(null); break;
            case Key.OemMinus or Key.Subtract when ctrl: _viewModel.ZoomOutCommand.Execute(null); break;
            case Key.D0 or Key.NumPad0 when ctrl: _viewModel.ResetZoomCommand.Execute(null); break;
            default: return;
        }
        e.Handled = true;
    }

    // Espacio / Av Pág: baja un trozo de pantalla; si ya está abajo del todo, pasa de página.
    private void ScreenDown()
    {
        ScrollViewer scroller = _viewModel.Layout == PageLayout.Webtoon ? WebtoonScroller! : PagedScroller;
        if (scroller.VerticalOffset + scroller.ViewportHeight < scroller.ExtentHeight - 1)
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset + scroller.ViewportHeight * 0.9);
        else if (_viewModel.Layout != PageLayout.Webtoon)
            _viewModel.NextCommand.Execute(null);
    }

    private void ScreenUp()
    {
        ScrollViewer scroller = _viewModel.Layout == PageLayout.Webtoon ? WebtoonScroller! : PagedScroller;
        if (scroller.VerticalOffset > 1)
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - scroller.ViewportHeight * 0.9);
        else if (_viewModel.Layout != PageLayout.Webtoon)
            _viewModel.PreviousCommand.Execute(null);
    }

    // ---- Ratón ----

    // Rueda: si la página cabe entera, pasa página; si no, desplaza. Ctrl + rueda: zoom.
    private void OnPagedWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            (e.Delta > 0 ? _viewModel.ZoomInCommand : _viewModel.ZoomOutCommand).Execute(null);
            e.Handled = true;
            return;
        }
        bool scrollable = PagedScroller.ExtentHeight > PagedScroller.ViewportHeight + 1;
        bool atEdge = e.Delta < 0
            ? PagedScroller.VerticalOffset + PagedScroller.ViewportHeight >= PagedScroller.ExtentHeight - 1
            : PagedScroller.VerticalOffset <= 1;
        if (!scrollable || atEdge)
        {
            if (e.Delta < 0) _viewModel.NextCommand.Execute(null); else _viewModel.PreviousCommand.Execute(null);
            if (scrollable && e.Delta > 0)
                Dispatcher.BeginInvoke(() => PagedScroller.ScrollToBottom(), DispatcherPriority.Loaded); // al volver atrás, se ve el final
            e.Handled = true;
        }
    }

    private void OnWebtoonWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;
        (e.Delta > 0 ? _viewModel.ZoomInCommand : _viewModel.ZoomOutCommand).Execute(null);
        e.Handled = true;
    }

    private void OnPageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && source.FindAncestor<ScrollBar>() is not null)
            return; // clic en la barra de desplazamiento
        if (_viewModel.IsMarking && PageViewAt(e) is { } view)
        {
            // "Marcar zona": se empieza a dibujar un rectángulo sobre esta página.
            _regionView = view;
            _regionStart = e.GetPosition(view);
            _regionPreview = new Rectangle
            {
                Stroke = (Brush)FindResource("Brush.Highlight.Yellow"),
                StrokeThickness = 2,
                StrokeDashArray = [4, 2],
            };
            ((Canvas)view.Children[1]).Children.Add(_regionPreview);
            PagedScroller.CaptureMouse();
            e.Handled = true;
            return;
        }
        _dragStart = e.GetPosition(PagedScroller);
        _scrollStart = new Point(PagedScroller.HorizontalOffset, PagedScroller.VerticalOffset);
        _dragged = false;
        PagedScroller.CaptureMouse();
    }

    // Arrastrar mueve una página ampliada (como con el dedo en el móvil).
    private void OnPageMouseMove(object sender, MouseEventArgs e)
    {
        if (_regionView is not null && _regionPreview is not null)
        {
            Point now = e.GetPosition(_regionView);
            Canvas.SetLeft(_regionPreview, Math.Min(now.X, _regionStart.X));
            Canvas.SetTop(_regionPreview, Math.Min(now.Y, _regionStart.Y));
            _regionPreview.Width = Math.Abs(now.X - _regionStart.X);
            _regionPreview.Height = Math.Abs(now.Y - _regionStart.Y);
            return;
        }
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
            return;
        Vector delta = e.GetPosition(PagedScroller) - start;
        if (!_dragged && delta.Length < DragThreshold)
            return;
        _dragged = true;
        Cursor = Cursors.SizeAll;
        PagedScroller.ScrollToHorizontalOffset(_scrollStart.X - delta.X);
        PagedScroller.ScrollToVerticalOffset(_scrollStart.Y - delta.Y);
    }

    // Un clic (sin arrastrar) en el lado izquierdo o derecho pasa la página.
    private void OnPageMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_regionView is { } regionView)
        {
            FinishRegion(regionView, e.GetPosition(regionView));
            return;
        }
        if (_dragStart is null)
            return;
        PagedScroller.ReleaseMouseCapture();
        Cursor = null;
        bool wasDrag = _dragged;
        _dragStart = null;
        if (wasDrag)
            return;
        // Clic sobre una zona marcada: abrir su nota (en vez de pasar la página).
        if (RegionAt(e) is { } region)
        {
            EditNote(region);
            return;
        }
        double x = e.GetPosition(PageArea).X / Math.Max(1, PageArea.ActualWidth);
        if (x < TurnZone) _viewModel.GoLeft();
        else if (x > 1 - TurnZone) _viewModel.GoRight();
    }

    // ---- Zonas marcadas ----

    // Al soltar: si el rectángulo tiene tamaño, se pide la nota y se guarda.
    private void FinishRegion(Grid view, Point end)
    {
        PagedScroller.ReleaseMouseCapture();
        ((Canvas)view.Children[1]).Children.Remove(_regionPreview);
        _regionView = null;
        _regionPreview = null;
        NormalizedRect? rect = NormalizedRect.From(
            _regionStart.X / view.ActualWidth, _regionStart.Y / view.ActualHeight,
            end.X / view.ActualWidth, end.Y / view.ActualHeight);
        if (rect is null || view.Tag is not int page)
            return; // fue un clic, no un rectángulo
        var dialog = new NoteDialog(this, $"Zona marcada · página {page + 1}", "", HighlightColor.Yellow);
        dialog.DeleteButton.Visibility = Visibility.Collapsed; // aún no existe: no hay nada que eliminar
        if (dialog.Ask() == NoteDialogResult.Save)
            _viewModel.AddRegion(page, rect.Value, dialog.Note, dialog.Color);
    }

    // Dibuja los rectángulos guardados de las páginas que se ven.
    private void DrawRegions()
    {
        foreach (Grid view in SpreadPanel.Children.OfType<Grid>())
        {
            var canvas = (Canvas)view.Children[1];
            canvas.Children.Clear();
            if (view.Tag is not int page)
                continue;
            foreach (AnnotationItemViewModel item in _viewModel.RegionsOn(page))
            {
                if (item.Annotation.Rect is not { } r)
                    continue;
                var shape = new Rectangle
                {
                    Width = r.Width * view.Width,
                    Height = r.Height * view.Height,
                    Stroke = item.ColorBrush,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(((SolidColorBrush)item.ColorBrush).Color) { Opacity = 0.18 },
                    ToolTip = item.HasNote ? item.Note : "Zona marcada (clic para escribir una nota)",
                    Tag = item,
                };
                Canvas.SetLeft(shape, r.X * view.Width);
                Canvas.SetTop(shape, r.Y * view.Height);
                canvas.Children.Add(shape);
            }
        }
    }

    private Grid? PageViewAt(MouseEventArgs e) =>
        SpreadPanel.Children.OfType<Grid>().FirstOrDefault(v =>
        {
            Point p = e.GetPosition(v);
            return p.X >= 0 && p.Y >= 0 && p.X <= v.ActualWidth && p.Y <= v.ActualHeight;
        });

    private AnnotationItemViewModel? RegionAt(MouseEventArgs e) =>
        (e.OriginalSource as Rectangle)?.Tag as AnnotationItemViewModel;

    private void EditNote(AnnotationItemViewModel item)
    {
        var dialog = new NoteDialog(this, item.Text + (item.Annotation.Chapter is { Length: > 0 } c ? $" · {c}" : ""),
            item.Note, item.Annotation.Color, showColors: item.Annotation.Kind != AnnotationKind.Bookmark);
        switch (dialog.Ask())
        {
            case NoteDialogResult.Save:
                _viewModel.UpdateAnnotation(item, dialog.Note, dialog.Color);
                break;
            case NoteDialogResult.Delete:
                _viewModel.DeleteAnnotation(item);
                break;
        }
    }

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
            MessageBox.Show(this, "Este cómic aún no tiene zonas, notas ni marcadores.", "MOON");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar notas",
            FileName = string.Concat(_viewModel.Title.Split(System.IO.Path.GetInvalidFileNameChars())) + " - notas",
            Filter = "Markdown (Obsidian, Notion…)|*.md|CSV (Excel)|*.csv",
        };
        if (dialog.ShowDialog(this) == true)
            _viewModel.Export(dialog.FileName);
    }

    private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e) => JumpToSliderPage();

    private void OnSliderClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not Thumb)
            JumpToSliderPage();
    }

    private void JumpToSliderPage()
    {
        int page = (int)Math.Round(PageSlider.Value);
        _viewModel.GoToPage(page);
        if (_viewModel.Layout == PageLayout.Webtoon)
            ScrollWebtoonTo(page);
    }

    // ---- Ventana ----

    private void OnFullScreenClick(object sender, RoutedEventArgs e) => ToggleFullScreen();

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
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateTitleBar();

    private void UpdateTitleBar()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
            TitleBar.SetDark(hwnd, !_themes.IsLight);
    }
}
