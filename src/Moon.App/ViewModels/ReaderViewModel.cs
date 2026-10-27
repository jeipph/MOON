using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Annotations;
using Moon.Core.Dictionary;
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
    private readonly DictionaryService _dictionary;
    private readonly VoiceService _voice;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly Timer _saveTimer;
    private readonly Lock _pendingLock = new();
    private (double Fraction, string? Cfi)? _pending;

    public ReaderViewModel(Book book, string filePath, LibraryService library, SettingsService settings, ThemeService themes,
        DictionaryService dictionary, VoiceService voice)
    {
        Book = book;
        FilePath = filePath;
        _library = library;
        _dictionary = dictionary;
        _voice = voice;
        SelectedSleep = SleepOptions[0];
        _dictionary.Preload();
        _settings = settings;
        _themes = themes;
        Title = book.Title;
        Progress = book.Progress;
        _saveTimer = new Timer(_ => SavePending());
        _settings.Changed += OnSettingsChanged;
        _themes.Changed += OnSettingsChanged;
        foreach (Annotation annotation in library.GetAnnotations(book.Id))
            Annotations.Add(new AnnotationItemViewModel(annotation));
    }

    // ---- Subrayados, notas y marcadores ----

    /// <summary>Lo seleccionado ahora en la página (null si no hay nada).</summary>
    /// <param name="Paragraph">El párrafo entero, y <paramref name="Offset"/> dónde empieza lo seleccionado.</param>
    public record PageSelection(string Cfi, string Text, string Chapter, double Fraction, string Paragraph = "", int Offset = 0);

    // ---- Lectura en voz alta ----

    /// <summary>Opción de un desplegable: lo que se ve y su valor.</summary>
    public sealed record Choice<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

    private string _language = "";

    public IReadOnlyList<VoiceOption> Voices { get; } = VoiceService.Voices();

    public IReadOnlyList<Choice<double>> Speeds { get; } =
        [new("0,75×", 0.75), new("1×", 1.0), new("1,25×", 1.25), new("1,5×", 1.5)];

    public IReadOnlyList<Choice<int>> SleepOptions { get; } =
        [new("Sin temporizador", 0), new("Al terminar el capítulo", -1), new("15 min", 15), new("30 min", 30), new("60 min", 60)];

    /// <summary>La sesión de lectura (se crea la primera vez que se pulsa "Escuchar").</summary>
    [ObservableProperty]
    public partial ReadAloudSession? ReadAloud { get; private set; }

    /// <summary>Leyendo en voz alta. Activarlo empieza desde la página que se ve; desactivarlo para.</summary>
    [ObservableProperty]
    public partial bool IsListening { get; set; }

    partial void OnIsListeningChanged(bool value)
    {
        if (value && ReadAloud is not { IsActive: true })
            StartListening();
        else if (!value && ReadAloud is { IsActive: true } active)
            active.Stop();
    }

    public VoiceOption SelectedVoice
    {
        get => Voices.FirstOrDefault(v => v.Name == _settings.Current.Voice.Voice) ?? Voices[0];
        set
        {
            _settings.Update(s => s with { Voice = s.Voice with { Voice = value.Name } });
            if (ReadAloud is { } session)
            {
                session.Voice = value;
                if (session.IsActive)
                    session.Start(); // con la voz nueva desde la frase en pantalla
            }
        }
    }

    public Choice<double> SelectedSpeed
    {
        get => Speeds.MinBy(s => Math.Abs(s.Value - _settings.Current.Voice.Speed))!;
        set
        {
            _settings.Update(s => s with { Voice = s.Voice with { Speed = value.Value } });
            if (ReadAloud is { } session)
                session.Speed = value.Value;
        }
    }

    [ObservableProperty]
    public partial Choice<int>? SelectedSleep { get; set; }

    partial void OnSelectedSleepChanged(Choice<int>? value) => ReadAloud?.SetSleepTimer(value?.Value ?? 0);

    private void StartListening()
    {
        ReadAloud ??= CreateSession();
        ReadAloud.Voice = SelectedVoice;
        ReadAloud.Speed = SelectedSpeed.Value;
        ReadAloud.SetSleepTimer(SelectedSleep?.Value ?? 0);
        ReadAloud.Start();
    }

    [RelayCommand]
    private void PlayPause() => ReadAloud?.TogglePause();

    [RelayCommand]
    private void SkipSentence() => ReadAloud?.SkipSentence();

    [RelayCommand]
    private void RestartSentence() => ReadAloud?.RestartSentence();

    private ReadAloudSession CreateSession()
    {
        var session = new ReadAloudSession(_voice, message => SendToPage?.Invoke(message), Title, _language);
        session.PropertyChanged += (_, e) =>
        {
            // Al terminar el libro la sesión se para sola: el botón "Escuchar" se desmarca.
            if (e.PropertyName == nameof(ReadAloudSession.IsActive) && !session.IsActive && session.Status.StartsWith("Fin"))
                IsListening = false;
        };
        return session;
    }

    public void OnTtsBlock(IEnumerable<(string? Mark, string Text)> items, string chapter) => ReadAloud?.OnBlock(items, chapter);

    /// <param name="error">Si la página falló al preparar las frases (se muestra en vez de "Fin del libro").</param>
    public void OnTtsEnd(string? error = null) => ReadAloud?.OnBookEnded(error);

    // ---- Diccionario ----

    /// <summary>Traducción de lo seleccionado (si es una palabra o una expresión corta).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLookup), nameof(LookupTitle), nameof(LookupSenses))]
    public partial LookupResult? Lookup { get; private set; }

    public bool HasLookup => Lookup is not null;

    public string LookupTitle => Lookup is null ? "" : Lookup.Lemma == EnglishLemmatizer.Clean(Lookup.Word) ? Lookup.Lemma : $"{Lookup.Word} → {Lookup.Lemma}";

    /// <summary>"verbo: ir, marchar" — como mucho tres categorías.</summary>
    public IEnumerable<string> LookupSenses => Lookup?.Senses.Take(3).Select(s => $"{s.PartOfSpeech}: {string.Join(", ", s.Translations.Take(5))}") ?? [];

    [ObservableProperty]
    public partial bool WordSaved { get; private set; }

    partial void OnSelectionChanged(PageSelection? value) => _ = LookUpSelectionAsync(value);

    private async Task LookUpSelectionAsync(PageSelection? selection)
    {
        WordSaved = false;
        string[] words = selection?.Text.Split((char[])[' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (selection is null || words.Length is 0 or > 3)
        {
            Lookup = null;
            return;
        }
        string? next = words.Length == 1 ? NextWord(selection.Paragraph, selection.Offset + selection.Text.Length) : words.ElementAtOrDefault(1);
        LookupResult? result = await _dictionary.LookupAsync(words[0], next);
        // Si se seleccionaron varias palabras y no son una expresión del diccionario, no se traduce.
        if (Selection == selection)
            Lookup = words.Length > 1 && result?.Lemma.Contains(' ') != true ? null : result;
    }

    // La palabra que sigue a la selección en el párrafo (para "give" + "up").
    private static string? NextWord(string paragraph, int from)
    {
        if (from >= paragraph.Length)
            return null;
        string rest = paragraph[from..].TrimStart();
        int end = rest.IndexOfAny([' ', '\n', '\t', ',', '.', ';', ':', '!', '?']);
        string next = end < 0 ? rest : rest[..end];
        return next.Length > 0 ? next : null;
    }

    [RelayCommand]
    private void SaveWord()
    {
        if (Lookup is not { } lookup || Selection is not { } s)
            return;
        _library.AddVocabulary(new VocabularyEntry(
            0, EnglishLemmatizer.Clean(lookup.Word), lookup.Lemma, lookup.Summary,
            VocabularyExporter.SentenceAround(s.Paragraph, s.Offset), Book.Id, Book.Title, s.Cfi, DateTime.Now));
        WordSaved = true;
    }

    public ObservableCollection<AnnotationItemViewModel> Annotations { get; } = [];

    [ObservableProperty]
    public partial bool IsNotesOpen { get; set; }

    [ObservableProperty]
    public partial PageSelection? Selection { get; set; }

    private string? _currentCfi;

    [RelayCommand]
    private void ToggleNotes() => IsNotesOpen = !IsNotesOpen;

    /// <summary>Subraya lo seleccionado con ese color.</summary>
    public AnnotationItemViewModel? HighlightSelection(HighlightColor color)
    {
        if (Selection is not { } s)
            return null;
        Annotation saved = _library.AddAnnotation(new Annotation(
            0, Book.Id, AnnotationKind.Highlight, s.Cfi, s.Text, "", color, s.Chapter, s.Fraction, DateTime.Now));
        var item = new AnnotationItemViewModel(saved);
        Insert(item);
        SendToPage?.Invoke(new { type = "highlight", cfi = s.Cfi, color = color.ToString() });
        Selection = null;
        return item;
    }

    /// <summary>Marcador en la página actual.</summary>
    [RelayCommand]
    private void AddBookmark()
    {
        if (_currentCfi is null)
            return;
        Annotation saved = _library.AddAnnotation(new Annotation(
            0, Book.Id, AnnotationKind.Bookmark, _currentCfi, "", "", HighlightColor.Blue, Chapter, Progress, DateTime.Now));
        Insert(new AnnotationItemViewModel(saved));
        IsNotesOpen = true;
    }

    public void UpdateAnnotation(AnnotationItemViewModel item, string note, HighlightColor color)
    {
        _library.UpdateAnnotation(item.Annotation.Id, note, color);
        int index = Annotations.IndexOf(item);
        var updated = new AnnotationItemViewModel(item.Annotation with { Note = note, Color = color });
        if (index >= 0)
            Annotations[index] = updated;
        if (item.Annotation.Kind == AnnotationKind.Highlight && color != item.Annotation.Color)
            SendToPage?.Invoke(new { type = "highlight", cfi = item.Annotation.Location, color = color.ToString() });
    }

    public void DeleteAnnotation(AnnotationItemViewModel item)
    {
        _library.DeleteAnnotation(item.Annotation.Id);
        Annotations.Remove(item);
        if (item.Annotation.Kind == AnnotationKind.Highlight)
            SendToPage?.Invoke(new { type = "remove-highlight", cfi = item.Annotation.Location });
    }

    public AnnotationItemViewModel? FindByLocation(string cfi) =>
        Annotations.FirstOrDefault(a => a.Annotation.Location == cfi);

    [RelayCommand]
    private void GoToAnnotation(AnnotationItemViewModel? item)
    {
        if (item is not null)
            SendToPage?.Invoke(new { type = "goTo", target = item.Annotation.Location });
    }

    /// <summary>Guarda las notas en Markdown (.md, para Obsidian) o CSV (.csv, para Excel).</summary>
    public void Export(string path)
    {
        var all = Annotations.Select(a => a.Annotation).ToList();
        string content = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? AnnotationExporter.ToCsv(Book, all)
            : AnnotationExporter.ToMarkdown(Book, all);
        System.IO.File.WriteAllText(path, content);
    }

    // En el orden del libro, como en el panel.
    private void Insert(AnnotationItemViewModel item)
    {
        int index = 0;
        while (index < Annotations.Count && Annotations[index].Annotation.Progress <= item.Annotation.Progress)
            index++;
        Annotations.Insert(index, item);
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
        annotations = Annotations
            .Where(a => a.Annotation.Kind == AnnotationKind.Highlight)
            .Select(a => new { cfi = a.Annotation.Location, color = a.Annotation.Color.ToString() }),
    };

    // ---- Mensajes de la página ----

    public void OnReady(string? title, IEnumerable<TocEntry> toc, string? language = null)
    {
        _language = language ?? "";
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
        _currentCfi = cfi ?? _currentCfi;
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
        ReadAloud?.Dispose();
        SavePending(); // lo último leído no se pierde al cerrar
    }
}
