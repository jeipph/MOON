using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Library;
using Moon.Core.Models;

namespace Moon.App.ViewModels;

/// <summary>Una entrada del índice del libro.</summary>
public sealed record TocEntry(string Label, string Href, int Depth)
{
    // Lo que leen los lectores de pantalla y la automatización.
    public override string ToString() => Label;
}

/// <summary>
/// Ventana de lectura: opciones de la página, índice y progreso. La página en sí la dibuja
/// foliate-js dentro de WebView2; esta clase le manda órdenes con <see cref="SendToPage"/>.
/// </summary>
public sealed partial class ReaderViewModel : ObservableObject, IDisposable
{
    // Guardar la posición en cada página sería escribir en disco sin parar: se espera un poco.
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly Timer _saveTimer;
    private readonly Lock _pendingLock = new();
    private (double Fraction, string? Cfi)? _pending;

    public ReaderViewModel(Book book, string filePath, LibraryService library, SettingsService settings, ThemeService themes)
    {
        Book = book;
        FilePath = filePath;
        _library = library;
        _settings = settings;
        _themes = themes;
        Title = book.Title;
        Progress = book.Progress;
        _saveTimer = new Timer(_ => SavePending());
        _settings.Changed += OnSettingsChanged;
        _themes.Changed += OnSettingsChanged;
    }

    public Book Book { get; }

    public string FilePath { get; }

    /// <summary>La ventana conecta aquí el envío de mensajes a la página.</summary>
    public Action<object>? SendToPage { get; set; }

    public ObservableCollection<TocEntry> Toc { get; } = [];

    [ObservableProperty]
    public partial string Title { get; private set; }

    [ObservableProperty]
    public partial string Chapter { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial double Progress { get; set; }

    public string ProgressText => $"{Math.Round(Progress * 100)} %";

    [ObservableProperty]
    public partial string? CurrentHref { get; private set; }

    [ObservableProperty]
    public partial bool IsTocOpen { get; set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    private ReaderSettings Reader => _settings.Current.Reader;

    public string FontSizeText => $"{Reader.FontSize} %";

    public PageFont Font
    {
        get => Reader.Font;
        set => ChangeReader(r => r with { Font = value });
    }

    public PageMargins Margins
    {
        get => Reader.Margins;
        set => ChangeReader(r => r with { Margins = value });
    }

    public PageTheme PageTheme
    {
        get => Reader.Theme;
        set => ChangeReader(r => r with { Theme = value });
    }

    /// <summary>
    /// "Páginas" o "Continuo". Va como texto y no como sí/no a propósito: WPF desmarca
    /// RadioButtons por su cuenta (al crear o cerrar ventanas) y con un sí/no eso guardaba "no".
    /// Con el conversor, solo cuenta marcar una opción, nunca desmarcarla.
    /// </summary>
    public string Flow
    {
        get => Reader.Paginated ? "Paginated" : "Scrolled";
        set => ChangeReader(r => r with { Paginated = value == "Paginated" });
    }

    /// <summary>Lo que se le manda a la página al abrir el libro.</summary>
    public object OpenMessage(string url) => new
    {
        type = "open",
        url,
        name = System.IO.Path.GetFileName(FilePath),
        location = Book.Location,
        settings = PageSettings(),
    };

    // ---- Mensajes de la página ----

    public void OnReady(string? title, IEnumerable<TocEntry> toc)
    {
        if (!string.IsNullOrWhiteSpace(title) && Book.Title == System.IO.Path.GetFileNameWithoutExtension(FilePath))
            Title = title; // el archivo no traía título pero el libro sí
        Toc.Clear();
        foreach (TocEntry entry in toc)
            Toc.Add(entry);
    }

    public void OnRelocate(double fraction, string? cfi, string chapter, string? href)
    {
        Progress = fraction;
        Chapter = chapter;
        CurrentHref = href;
        lock (_pendingLock)
            _pending = (fraction, cfi);
        _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    public void OnError(string message) =>
        Error = $"No se pudo abrir este libro: {message}";

    // ---- Órdenes ----

    [RelayCommand]
    private void Next() => SendToPage?.Invoke(new { type = "next" });

    [RelayCommand]
    private void Previous() => SendToPage?.Invoke(new { type = "prev" });

    [RelayCommand]
    private void GoTo(TocEntry? entry)
    {
        if (entry is null)
            return;
        SendToPage?.Invoke(new { type = "goTo", target = entry.Href });
    }

    /// <summary>Al soltar la barra de progreso.</summary>
    public void GoToFraction(double fraction) =>
        SendToPage?.Invoke(new { type = "goToFraction", value = Math.Clamp(fraction, 0, 1) });

    [RelayCommand]
    private void ToggleToc() => IsTocOpen = !IsTocOpen;

    [RelayCommand]
    private void BiggerText() => ChangeReader(r => r.WithFontSize(r.FontSize + ReaderSettings.FontSizeStep));

    [RelayCommand]
    private void SmallerText() => ChangeReader(r => r.WithFontSize(r.FontSize - ReaderSettings.FontSizeStep));

    [RelayCommand]
    private void ResetText() => ChangeReader(r => r.WithFontSize(100));

    private void ChangeReader(Func<ReaderSettings, ReaderSettings> change) =>
        _settings.Update(s => s with { Reader = change(s.Reader) });

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FontSizeText));
        OnPropertyChanged(nameof(Font));
        OnPropertyChanged(nameof(Margins));
        OnPropertyChanged(nameof(PageTheme));
        OnPropertyChanged(nameof(Flow));
        SendToPage?.Invoke(new { type = "settings", settings = PageSettings() });
    }

    /// <summary>Opciones en el formato que entiende reader.js.</summary>
    private object PageSettings()
    {
        ReaderSettings r = Reader;
        PageTheme theme = r.Theme == PageTheme.Auto
            ? (_themes.IsLight ? PageTheme.Light : PageTheme.Dark)
            : r.Theme;
        return new
        {
            fontSize = r.FontSize,
            font = r.Font.ToString().ToLowerInvariant(),
            margins = r.Margins.ToString().ToLowerInvariant(),
            theme = theme.ToString().ToLowerInvariant(),
            paginated = r.Paginated,
        };
    }

    /// <summary>Colores de la ventana alrededor de la página (para que no haya un marco de otro color).</summary>
    public PageTheme EffectivePageTheme => Reader.Theme == PageTheme.Auto
        ? (_themes.IsLight ? PageTheme.Light : PageTheme.Dark)
        : Reader.Theme;

    private void SavePending()
    {
        (double Fraction, string? Cfi)? pending;
        lock (_pendingLock)
        {
            pending = _pending;
            _pending = null;
        }
        if (pending is { } p)
            _library.SetProgress(Book.Id, p.Fraction, p.Cfi);
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _themes.Changed -= OnSettingsChanged;
        _saveTimer.Dispose();
        SavePending(); // lo último leído no se pierde al cerrar
    }
}
