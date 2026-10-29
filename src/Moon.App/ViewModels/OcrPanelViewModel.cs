using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Annotations;
using Moon.Core.Comics;
using Moon.Core.Dictionary;
using Moon.Core.Library;
using Moon.Core.Ocr;

namespace Moon.App.ViewModels;

/// <summary>
/// "Leer texto" en cómics: se recuadra un bocadillo, el OCR de Windows lee su texto (sin internet)
/// y aparece en un panel editable donde cada palabra se puede buscar en el diccionario y guardar.
/// </summary>
public sealed partial class OcrPanelViewModel : ObservableObject
{
    // Paquete de OCR en inglés de Windows (para el aviso cuando falta).
    public const string EnglishCapability = "Language.OCR~~~en-US~0.0.1.0";

    private readonly IOcrEngine _engine;
    private readonly PageImages _pages;
    private readonly DictionaryService _dictionary;
    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly Book _book;
    private int _requestVersion;
    private string _lookupSentence = "";

    public OcrPanelViewModel(IOcrEngine engine, PageImages pages, DictionaryService dictionary, LibraryService library, SettingsService settings, Book book)
    {
        _engine = engine;
        _pages = pages;
        _dictionary = dictionary;
        _library = library;
        _settings = settings;
        _book = book;
        Languages = engine.Languages;
        string saved = settings.Current.OcrLanguage;
        Language = Languages.FirstOrDefault(l => l.Tag == saved)
            ?? Languages.FirstOrDefault(l => l.IsEnglish)
            ?? Languages.FirstOrDefault();
    }

    public IReadOnlyList<OcrLanguage> Languages { get; }

    /// <summary>Sin inglés el OCR lee peor los cómics en inglés (p. ej. "CAN'T" como "CANIT").</summary>
    public bool IsEnglishMissing => !Languages.Any(l => l.IsEnglish);

    [ObservableProperty]
    public partial OcrLanguage? Language { get; set; }

    partial void OnLanguageChanged(OcrLanguage? value)
    {
        if (value is not null)
            _settings.Update(s => s with { OcrLanguage = value.Tag });
        if (IsOpen && LastRect is { } rect)
            _ = ReadAsync(Page, rect); // se vuelve a leer con el idioma nuevo
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>El texto leído; la persona puede corregirlo.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>Mensaje (sin texto, error, idioma que falta…) o "".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string Status { get; private set; } = "";

    public bool HasStatus => Status.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading))]
    public partial int Page { get; private set; }

    public string Heading => $"TEXTO · PÁGINA {Page + 1}";

    public NormalizedRect? LastRect { get; private set; }

    /// <summary>Se pide guardar el recuadro como zona marcada (con el texto de nota).</summary>
    public event Action<int, NormalizedRect, string>? SaveAsRegionRequested;

    public async Task ReadAsync(int page, NormalizedRect rect)
    {
        int version = ++_requestVersion;
        Page = page;
        LastRect = rect;
        IsOpen = true;
        Lookup = null;
        Text = "";
        if (Language is not { } language)
        {
            Status = "Windows no tiene ningún idioma de OCR instalado. Ve a Configuración → Hora e idioma → Idioma y región y añade «English (United States)».";
            return;
        }
        IsBusy = true;
        Status = "Leyendo…";
        try
        {
            BgraImage? image = await _pages.CropAsync(page, rect, _engine.MaxImageSize);
            if (image is null)
            {
                Finish(version, "", "No se pudo leer esta página (¿dañada?).");
                return;
            }
            IReadOnlyList<string> lines = await _engine.ReadLinesAsync(image, language.Tag);
            string text = OcrText.Clean(lines);
            Finish(version, text, text.Length == 0
                ? "No se encontró texto. Prueba a recuadrar solo el bocadillo, con algo de margen."
                : "");
        }
        catch (InvalidOperationException exception)
        {
            Finish(version, "", exception.Message);
        }
    }

    private void Finish(int version, string text, string status)
    {
        if (version != _requestVersion)
            return; // mientras leía se recuadró otro bocadillo
        Text = text;
        Status = status;
        IsBusy = false;
    }

    [RelayCommand]
    private void Close()
    {
        _requestVersion++;
        IsOpen = false;
        IsBusy = false;
        Lookup = null;
    }

    [RelayCommand]
    private void Copy()
    {
        if (Text.Length > 0)
            System.Windows.Clipboard.SetText(Text);
    }

    [RelayCommand]
    private void SaveAsRegion()
    {
        if (LastRect is { } rect)
            SaveAsRegionRequested?.Invoke(Page, rect, Text.Trim());
    }

    // ---- Diccionario ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLookup), nameof(LookupTitle), nameof(LookupSenses))]
    public partial LookupResult? Lookup { get; private set; }

    public bool HasLookup => Lookup is not null;

    public string LookupTitle => Lookup is null ? "" : Lookup.Lemma == EnglishLemmatizer.Clean(Lookup.Word) ? Lookup.Lemma : $"{Lookup.Word} → {Lookup.Lemma}";

    public IEnumerable<string> LookupSenses => Lookup?.Senses.Take(3).Select(s => $"{s.PartOfSpeech}: {string.Join(", ", s.Translations.Take(5))}") ?? [];

    [ObservableProperty]
    public partial bool WordSaved { get; private set; }

    /// <summary>Lo seleccionado en el texto, o la palabra donde está el cursor si no hay selección.</summary>
    /// <param name="caret">Dónde empieza la selección (o el cursor).</param>
    public async Task LookUpAsync(string selected, int caret)
    {
        WordSaved = false;
        string? word, next;
        string[] words = selected.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is > 0 and <= 3)
            // Una palabra: se mira también la siguiente del texto ("give" + "up").
            (word, next) = (words[0], words.Length > 1 ? words[1] : OcrText.WordAt(Text, caret)?.Next);
        else if (words.Length == 0 && OcrText.WordAt(Text, caret) is { } found)
            (word, next) = found;
        else
        {
            Lookup = null;
            return;
        }
        _lookupSentence = VocabularyExporter.SentenceAround(Text, Math.Min(caret, Math.Max(0, Text.Length - 1)));
        LookupResult? result = await _dictionary.LookupAsync(word, next);
        Lookup = words.Length > 1 && result?.Lemma.Contains(' ') != true ? null : result;
    }

    [RelayCommand]
    private void SaveWord()
    {
        if (Lookup is not { } lookup)
            return;
        _library.AddVocabulary(new VocabularyEntry(
            0, EnglishLemmatizer.Clean(lookup.Word), lookup.Lemma, lookup.Summary,
            _lookupSentence, _book.Id, _book.Title, ComicView.PageLocation(Page), DateTime.Now));
        WordSaved = true;
    }
}
