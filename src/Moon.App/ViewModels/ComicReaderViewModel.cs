using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Comics;
using Moon.Core.Library;
using Moon.Core.Models;

namespace Moon.App.ViewModels;

/// <summary>
/// Lector de cómics y manga: qué páginas se ven, cómo (una, doble, webtoon), en qué dirección
/// y con qué ajustes de imagen. La ventana dibuja; esta clase decide.
/// </summary>
public sealed partial class ComicReaderViewModel : ObservableObject, IDisposable
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 4.0;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly Timer _saveTimer;
    private IReadOnlyList<int[]> _spreads = [];
    private ComicView _view;
    private int _pendingSavePage = -1;

    public ComicReaderViewModel(Book book, PageImages pages, LibraryService library, SettingsService settings, bool isManga)
    {
        Book = book;
        Pages = pages;
        _library = library;
        _settings = settings;
        _view = Core.Comics.ComicView.FromJson(book.ComicView) ?? Core.Comics.ComicView.Default(isManga);
        Title = book.Title;
        _saveTimer = new Timer(_ => SaveProgress());
        RebuildSpreads(ComicView.PageFromLocation(book.Location) ?? 0);
        Pages.SizesKnown += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => RebuildSpreads(CurrentPage));
    }

    public Book Book { get; }

    public PageImages Pages { get; }

    public string Title { get; }

    public int PageCount => Pages.Count;

    public int LastPage => PageCount - 1;

    /// <summary>Hay que volver a dibujar (cambió la página, la vista o los ajustes).</summary>
    public event EventHandler? ViewChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageText))]
    public partial int CurrentPage { get; private set; }

    public string PageText
    {
        get
        {
            int[] spread = CurrentSpread;
            string pages = spread.Length == 2 ? $"{spread[0] + 1}-{spread[1] + 1}" : $"{CurrentPage + 1}";
            return $"{pages} / {PageCount}";
        }
    }

    /// <summary>Las páginas que se ven ahora, en orden de lectura.</summary>
    public int[] CurrentSpread => _spreads.Count == 0 ? [0] : _spreads[SpreadLayout.SpreadOf(_spreads, CurrentPage)];

    [ObservableProperty]
    public partial double Zoom { get; private set; } = 1.0;

    public string ZoomText => $"{Math.Round(Zoom * 100)} %";

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomText));
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Vista (igual para todos los cómics) ----

    private ComicSettings Comic => _settings.Current.Comic;

    public PageLayout Layout
    {
        get => Comic.Layout;
        set
        {
            if (value == Comic.Layout)
                return;
            _settings.Update(s => s with { Comic = s.Comic with { Layout = value } });
            OnPropertyChanged();
            RebuildSpreads(CurrentPage);
        }
    }

    public ComicFit Fit
    {
        get => Comic.Fit;
        set
        {
            if (value == Comic.Fit)
                return;
            _settings.Update(s => s with { Comic = s.Comic with { Fit = value } });
            OnPropertyChanged();
            Zoom = 1.0;
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ---- Propio de este cómic ----

    public ReadingDirection Direction
    {
        get => _view.Direction;
        set => ChangeView(_view with { Direction = value }, nameof(Direction));
    }

    public bool IsRightToLeft => Direction == ReadingDirection.RightToLeft;

    public int Brightness
    {
        get => _view.Adjustments.Brightness;
        set => ChangeView(_view with { Adjustments = _view.Adjustments with { Brightness = value } }, nameof(Brightness));
    }

    public int Contrast
    {
        get => _view.Adjustments.Contrast;
        set => ChangeView(_view with { Adjustments = _view.Adjustments with { Contrast = value } }, nameof(Contrast));
    }

    public int Gamma
    {
        get => _view.Adjustments.Gamma;
        set => ChangeView(_view with { Adjustments = _view.Adjustments with { Gamma = value } }, nameof(Gamma));
    }

    public bool Whiten
    {
        get => _view.Adjustments.Whiten;
        set => ChangeView(_view with { Adjustments = _view.Adjustments with { Whiten = value } }, nameof(Whiten));
    }

    public ImageAdjustments Adjustments => _view.Adjustments;

    private void ChangeView(ComicView view, string property)
    {
        if (view == _view)
            return;
        _view = view;
        _library.SetComicView(Book.Id, view);
        OnPropertyChanged(property);
        OnPropertyChanged(nameof(IsRightToLeft));
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ResetAdjustments()
    {
        ChangeView(_view with { Adjustments = ImageAdjustments.None }, nameof(Adjustments));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Contrast));
        OnPropertyChanged(nameof(Gamma));
        OnPropertyChanged(nameof(Whiten));
    }

    // ---- Moverse por el cómic ----

    /// <summary>Siguiente en orden de lectura.</summary>
    [RelayCommand]
    private void Next() => GoToSpread(SpreadLayout.SpreadOf(_spreads, CurrentPage) + 1);

    [RelayCommand]
    private void Previous() => GoToSpread(SpreadLayout.SpreadOf(_spreads, CurrentPage) - 1);

    /// <summary>Flecha o clic a la izquierda: en manga avanza, en cómic retrocede.</summary>
    public void GoLeft()
    {
        if (IsRightToLeft) Next(); else Previous();
    }

    public void GoRight()
    {
        if (IsRightToLeft) Previous(); else Next();
    }

    [RelayCommand]
    private void First() => GoToPage(0);

    [RelayCommand]
    private void Last() => GoToPage(LastPage);

    public void GoToPage(int page)
    {
        page = Math.Clamp(page, 0, LastPage);
        if (Layout != PageLayout.Webtoon)
            page = _spreads[SpreadLayout.SpreadOf(_spreads, page)][0];
        SetPage(page);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>En webtoon la página cambia al desplazarse; no hay que volver a dibujar.</summary>
    public void ReportVisiblePage(int page) => SetPage(Math.Clamp(page, 0, LastPage));

    private void GoToSpread(int spread)
    {
        if (spread < 0 || spread >= _spreads.Count)
            return;
        SetPage(_spreads[spread][0]);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetPage(int page)
    {
        if (page == CurrentPage)
            return;
        CurrentPage = page;
        Interlocked.Exchange(ref _pendingSavePage, page);
        _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void RebuildSpreads(int keepPage)
    {
        _spreads = SpreadLayout.Build(PageCount, Layout, Pages.IsWide, Comic.CoverAlone);
        CurrentPage = -1; // fuerza el aviso aunque sea la misma página
        GoToPage(keepPage);
    }

    // ---- Zoom ----

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(MaxZoom, Math.Round(Zoom * 1.25, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(MinZoom, Math.Round(Zoom / 1.25, 2));

    [RelayCommand]
    private void ResetZoom() => Zoom = 1.0;

    // ---- Guardar ----

    private void SaveProgress()
    {
        int page = Interlocked.Exchange(ref _pendingSavePage, -1);
        if (page < 0)
            return;
        int lastVisible = Layout == PageLayout.Webtoon ? page : _spreads[SpreadLayout.SpreadOf(_spreads, page)].Max();
        _library.SetProgress(Book.Id, (lastVisible + 1.0) / PageCount, ComicView.PageLocation(page));
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        SaveProgress(); // lo último leído no se pierde al cerrar
        Pages.Dispose();
    }
}
