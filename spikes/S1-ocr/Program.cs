// S1: ¿funciona Windows.Media.Ocr en una app sin empaquetar (sin MSIX)?
// Uso: dotnet run -- <imagen>            lee la imagen entera
//      dotnet run -- <cómic> <página>    lee una página de un CBZ/CBR/CB7 (empieza en 1)
//      dotnet run -- <cómic> <página> x y ancho alto   solo ese recuadro (proporciones 0–1)
//      dotnet run -- <cómic> <página> --guardar <archivo>   guarda la página para verla
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Moon.Core.Comics;
using Moon.Core.Ocr;
using Moon.Windows.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

var engine = new WindowsOcrEngine();
Console.WriteLine("Idiomas de OCR instalados:");
foreach (OcrLanguage language in engine.Languages)
    Console.WriteLine($"  {language.Tag} ({language.Name})");
Console.WriteLine($"Tamaño máximo de imagen: {engine.MaxImageSize} px");

if (args.Length == 0)
    return;

byte[] bytes;
if (args.Length > 1)
{
    using IPageSource comic = PageSources.OpenArchive(args[0]);
    bytes = comic.Read(int.Parse(args[1]) - 1, 0);
}
else
    bytes = await File.ReadAllBytesAsync(args[0]);

if (args.Length > 2 && args[2] == "--guardar")
{
    await File.WriteAllBytesAsync(args[3], bytes);
    return;
}

using var stream = new InMemoryRandomAccessStream();
await stream.WriteAsync(bytes.AsBuffer());
stream.Seek(0);
BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
var transform = new BitmapTransform();
uint width = decoder.PixelWidth, height = decoder.PixelHeight;
// Con x y ancho alto (0–1) se lee solo ese recuadro, agrandado como en MOON.
if (args.Length > 5)
{
    double[] r = [.. args[2..6].Select(a => double.Parse(a, System.Globalization.CultureInfo.InvariantCulture))];
    PixelRect crop = OcrText.ToPixels(new Moon.Core.Annotations.NormalizedRect(r[0], r[1], r[2], r[3]), (int)width, (int)height);
    double scale = args.Length > 6 ? double.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture)
        : OcrText.Scale(crop.Width, crop.Height, engine.MaxImageSize);
    transform.ScaledWidth = (uint)(width * scale);
    transform.ScaledHeight = (uint)(height * scale);
    transform.Bounds = new BitmapBounds { X = (uint)(crop.X * scale), Y = (uint)(crop.Y * scale), Width = (uint)(crop.Width * scale), Height = (uint)(crop.Height * scale) };
    (width, height) = (transform.Bounds.Width, transform.Bounds.Height);
}
PixelDataProvider data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
    transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
var image = new BgraImage(data.DetachPixelData(), (int)width, (int)height);

foreach (OcrLanguage language in engine.Languages)
{
    var watch = Stopwatch.StartNew();
    IReadOnlyList<string> lines = await engine.ReadLinesAsync(image, language.Tag);
    Console.WriteLine($"\n[{language.Tag}] {image.Width}×{image.Height}, {watch.ElapsedMilliseconds} ms, {lines.Count} líneas");
    Console.WriteLine(OcrText.Clean(lines));
}
