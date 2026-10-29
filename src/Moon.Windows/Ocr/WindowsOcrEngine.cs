using System.Runtime.InteropServices.WindowsRuntime;
using Moon.Core.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Moon.Windows.Ocr;

/// <summary>
/// El OCR que trae Windows (<c>Windows.Media.Ocr</c>): sin descargas ni internet.
/// Funciona sin empaquetar la app (probado en el spike S1). Lee los idiomas cuyo paquete de OCR
/// está instalado en Configuración → Hora e idioma.
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly Dictionary<string, OcrEngine> _engines = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<OcrLanguage> Languages { get; } =
        [.. OcrEngine.AvailableRecognizerLanguages.Select(l => new OcrLanguage(l.LanguageTag, l.DisplayName))];

    public int MaxImageSize => (int)OcrEngine.MaxImageDimension;

    public async Task<IReadOnlyList<string>> ReadLinesAsync(BgraImage image, string languageTag)
    {
        OcrEngine engine = EngineFor(languageTag);
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, image.Width, image.Height, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(image.Pixels.AsBuffer());
        OcrResult result = await engine.RecognizeAsync(bitmap);
        return [.. result.Lines.Select(l => l.Text)];
    }

    // Crear el motor tarda un poco: uno por idioma, y se reutiliza.
    private OcrEngine EngineFor(string tag)
    {
        lock (_lock)
        {
            if (_engines.TryGetValue(tag, out OcrEngine? engine))
                return engine;
            engine = OcrEngine.TryCreateFromLanguage(new Language(tag))
                ?? throw new InvalidOperationException($"El idioma de OCR «{tag}» no está instalado en Windows.");
            _engines[tag] = engine;
            return engine;
        }
    }
}
