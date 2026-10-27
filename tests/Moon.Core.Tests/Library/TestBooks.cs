using System.IO.Compression;
using System.Text;

namespace Moon.Core.Tests.Library;

/// <summary>Crea libros pequeños de prueba (EPUB y CBZ) en una carpeta temporal.</summary>
internal static class TestBooks
{
    // Un PNG de 1×1 píxel.
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    public static string Epub(string folder, string fileName, string title = "El nombre del viento",
        string author = "Patrick Rothfuss", bool epub3Cover = true)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "mimetype", "application/epub+zip");
        Add(zip, "META-INF/container.xml", """
            <?xml version="1.0"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
            </container>
            """);
        string coverItem = epub3Cover
            ? """<item id="img1" href="images/portada%20grande.png" media-type="image/png" properties="cover-image"/>"""
            : """<item id="img1" href="images/portada%20grande.png" media-type="image/png"/>""";
        string coverMeta = epub3Cover ? "" : """<meta name="cover" content="img1"/>""";
        Add(zip, "OEBPS/content.opf", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>{title}</dc:title>
                <dc:creator>{author}</dc:creator>
                <meta name="calibre:series" content="Crónica del asesino de reyes"/>
                {coverMeta}
              </metadata>
              <manifest>{coverItem}</manifest>
            </package>
            """);
        zip.CreateEntry("OEBPS/images/portada grande.png").Open().Using(s => s.Write(Png));
        return path;
    }

    public static string Cbz(string folder, string fileName, bool withComicInfo = true)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        if (withComicInfo)
        {
            Add(zip, "ComicInfo.xml", """
                <?xml version="1.0"?>
                <ComicInfo><Series>Saga</Series><Number>3</Number><Writer>Brian K. Vaughan</Writer></ComicInfo>
                """);
        }
        // Orden natural: la portada es "pag2", no "pag10"; las carpetas de macOS se ignoran.
        foreach (string name in new[] { "__MACOSX/pag1.png", "pag10.png", "pag2.png" })
            zip.CreateEntry(name).Open().Using(s => s.Write(name == "pag2.png" ? Png : [1, 2, 3]));
        return path;
    }

    public static string Text(string folder, string fileName, string content)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static void Add(ZipArchive zip, string name, string text) =>
        zip.CreateEntry(name).Open().Using(s => s.Write(Encoding.UTF8.GetBytes(text)));

    private static void Using(this Stream stream, Action<Stream> action)
    {
        using (stream)
            action(stream);
    }
}
