// S1: ¿funciona Windows.Media.Ocr en una app sin empaquetar (sin MSIX)? Usa el mismo motor que MOON.
// Uso: dotnet run -- <imagen>            lee la imagen entera
//      dotnet run -- <cómic> <página>    lee una página de un CBZ/CBR/CB7 (empieza en 1)
//      dotnet run -- <cómic> <página> x y ancho alto   solo ese recuadro (proporciones 0–1)
//      dotnet run -- <cómic> <página> --guardar <archivo>   guarda la página para verla
//      dotnet run -- --preparar <datos> <carpeta>   copia de prueba con esa carpeta enlazada (MOON.exe --datos)
using System.Diagnostics;
using System.Globalization;
using Moon.Core.Annotations;
using Moon.Core.Comics;
using Moon.Core.Library;
using Moon.Core.Ocr;
using Moon.Windows.Comics;
using Moon.Windows.Ocr;

if (args.Length == 3 && args[0] == "--preparar")
{
    var library = new LibraryService(new SqliteLibraryStore(Path.Combine(args[1], "moon.db")),
        Path.Combine(args[1], "Biblioteca"), Path.Combine(args[1], "covers"),
        LibraryService.CurrentDrives, PdfPageSource.ReadMetadata);
    Console.WriteLine(library.Link(args[2]));
    return;
}

var engine = new WindowsOcrEngine();
Console.WriteLine("Idiomas de OCR instalados:");
foreach (OcrLanguage language in engine.Languages)
    Console.WriteLine($"  {language.Tag} ({language.Name})");

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

if (args.Length > 3 && args[2] == "--guardar")
{
    await File.WriteAllBytesAsync(args[3], bytes);
    return;
}

var rect = new NormalizedRect(0, 0, 1, 1);
if (args.Length > 5)
{
    double[] r = [.. args[2..6].Select(a => double.Parse(a, CultureInfo.InvariantCulture))];
    rect = new NormalizedRect(r[0], r[1], r[2], r[3]);
}

foreach (OcrLanguage language in engine.Languages)
{
    var watch = Stopwatch.StartNew();
    IReadOnlyList<string> lines = await engine.ReadLinesAsync(bytes, rect, language.Tag);
    Console.WriteLine($"\n[{language.Tag}] {watch.ElapsedMilliseconds} ms, {lines.Count} líneas");
    Console.WriteLine(OcrText.Clean(lines));
}
