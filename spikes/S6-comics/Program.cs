using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Readers;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

// Prueba S6: abrir el CBR (RAR) con SharpCompress y el PDF con el lector de Windows.
string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
string cbr = Path.Combine(downloads, "The Walking Dead Deluxe #001 Castellano.cbr");
string pdf = Path.Combine(downloads, "Tomo 2 Saga Sword&Shield.pdf");
string outDir = Path.Combine(AppContext.BaseDirectory, "salida");
Directory.CreateDirectory(outDir);

var sw = Stopwatch.StartNew();
using (IArchive archive = ArchiveFactory.OpenArchive(cbr, new ReaderOptions()))
{
    var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
    Console.WriteLine($"CBR: {entries.Count} archivos, sólido={archive.IsSolid}, tipo={archive.Type}, abrir {sw.ElapsedMilliseconds} ms");
    foreach (var e in entries.Take(4)) Console.WriteLine($"   {e.Key} ({e.Size / 1024} KB)");
    Console.WriteLine($"   ... {entries.Last().Key}");
    sw.Restart();
    var middle = entries.OrderBy(e => e.Key).ElementAt(entries.Count / 2);
    using (var s = middle.OpenEntryStream())
    using (var f = File.Create(Path.Combine(outDir, "cbr-medio" + Path.GetExtension(middle.Key))))
        s.CopyTo(f);
    Console.WriteLine($"   página del medio (acceso directo) en {sw.ElapsedMilliseconds} ms");
    sw.Restart();
    foreach (var e in entries) { using var es = e.OpenEntryStream(); es.CopyTo(Stream.Null); }
    Console.WriteLine($"   todas leídas en {sw.ElapsedMilliseconds} ms");
}

sw.Restart();
StorageFile file = await StorageFile.GetFileFromPathAsync(pdf);
PdfDocument doc = await PdfDocument.LoadFromFileAsync(file);
Console.WriteLine($"PDF: {doc.PageCount} páginas, abrir {sw.ElapsedMilliseconds} ms");
foreach (uint i in new uint[] { 0, 10 })
{
    sw.Restart();
    using PdfPage page = doc.GetPage(i);
    var stream = new InMemoryRandomAccessStream();
    await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 1400 });
    using var output = File.Create(Path.Combine(outDir, $"pdf-pag{i + 1}.png"));
    await stream.AsStreamForRead().CopyToAsync(output);
    Console.WriteLine($"   página {i + 1}: {page.Size.Width:F0}x{page.Size.Height:F0}, dibujada en {sw.ElapsedMilliseconds} ms ({output.Length / 1024} KB)");
}
Console.WriteLine("salida: " + outDir);
