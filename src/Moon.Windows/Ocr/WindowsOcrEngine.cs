using System.Runtime.InteropServices.WindowsRuntime;
using Moon.Core.Annotations;
using Moon.Core.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

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

    public async Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, NormalizedRect rect, string languageTag)
    {
        OcrEngine engine = EngineFor(languageTag);
        using var stream = new InMemoryRandomAccessStream();
        BitmapDecoder decoder;
        try
        {
            await stream.WriteAsync(image.AsBuffer());
            stream.Seek(0);
            decoder = await BitmapDecoder.CreateAsync(stream);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException("La página está dañada.", exception);
        }

        // Recorte y aumento con el decodificador de Windows, igual que en el spike S1: al agrandar
        // con WPF el OCR leía peor ("DIOS..." salía "nos...").
        PixelRect crop = OcrText.ToPixels(rect, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
        double scale = OcrText.Scale(crop.Width, crop.Height, (int)OcrEngine.MaxImageDimension);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Round(decoder.PixelWidth * scale),
            ScaledHeight = (uint)Math.Round(decoder.PixelHeight * scale),
        };
        transform.Bounds = new BitmapBounds
        {
            X = (uint)(crop.X * scale),
            Y = (uint)(crop.Y * scale),
            Width = Math.Min((uint)(crop.Width * scale), transform.ScaledWidth - (uint)(crop.X * scale)),
            Height = Math.Min((uint)(crop.Height * scale), transform.ScaledHeight - (uint)(crop.Y * scale)),
        };
        using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
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
