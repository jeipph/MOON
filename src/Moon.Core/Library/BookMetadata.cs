using System.IO.Compression;
using System.Xml.Linq;
using SharpCompress.Archives;
using ArchiveOptions = SharpCompress.Readers.ReaderOptions;

namespace Moon.Core.Library;

/// <summary>Título, autor y portada sacados del propio archivo (sin internet).</summary>
public sealed record BookMetadata(string Title, string? Author, string? Series, byte[]? Cover)
{
    /// <summary>Lee lo que se pueda; si el archivo está dañado, al menos devuelve el nombre del archivo como título.</summary>
    /// <param name="platformReader">Lector para formatos que necesitan Windows (PDF); null si no hay.</param>
    public static BookMetadata Read(string path, BookFormat format, Func<string, BookFormat, BookMetadata?>? platformReader = null)
    {
        BookMetadata fallback = FromFileName(path);
        try
        {
            BookMetadata? read = format switch
            {
                BookFormat.Epub => ReadEpub(path),
                BookFormat.Cbz or BookFormat.Cbr or BookFormat.Cb7 => ReadComic(path),
                _ => platformReader?.Invoke(path, format),
            };
            if (read is null)
                return fallback;
            return read with { Title = string.IsNullOrWhiteSpace(read.Title) ? fallback.Title : read.Title };
        }
        // El archivo viene de fuera y puede estar roto de mil maneras (cada librería lanza sus propios errores):
        // pase lo que pase, el libro entra en la biblioteca con el nombre del archivo.
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return fallback;
        }
    }

    /// <summary>"El_nombre_del_viento.epub" → "El nombre del viento".</summary>
    public static BookMetadata FromFileName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Trim();
        return new BookMetadata(name.Length > 0 ? name : Path.GetFileName(path), null, null, null);
    }

    // EPUB = ZIP con META-INF/container.xml → archivo OPF con los datos y la lista de archivos.
    private static BookMetadata? ReadEpub(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);
        XDocument? container = LoadXml(zip, "META-INF/container.xml");
        string? opfPath = container?.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value;
        if (opfPath is null)
            return null;
        XDocument? opf = LoadXml(zip, opfPath);
        if (opf is null)
            return null;

        XElement? metadata = opf.Descendants().FirstOrDefault(e => e.Name.LocalName == "metadata");
        string? title = FirstText(metadata, "title");
        string? author = FirstText(metadata, "creator");
        string? series = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "meta" && (string?)e.Attribute("name") == "calibre:series")?.Attribute("content")?.Value
            ?? metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "meta" && (string?)e.Attribute("property") == "belongs-to-collection")?.Value;

        string? coverHref = FindEpubCover(opf, metadata);
        byte[]? cover = coverHref is null ? null : ReadEntry(zip, Combine(opfPath, coverHref));
        return new BookMetadata(title?.Trim() ?? "", author?.Trim(), series?.Trim(), cover);
    }

    private static string? FindEpubCover(XDocument opf, XElement? metadata)
    {
        var items = opf.Descendants().Where(e => e.Name.LocalName == "item").ToList();

        // EPUB 3: <item properties="cover-image">
        XElement? item = items.FirstOrDefault(e => ((string?)e.Attribute("properties"))?.Split(' ').Contains("cover-image") == true);

        // EPUB 2: <meta name="cover" content="id-de-la-imagen">
        if (item is null)
        {
            string? coverId = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "meta" && (string?)e.Attribute("name") == "cover")?.Attribute("content")?.Value;
            item = items.FirstOrDefault(e => (string?)e.Attribute("id") == coverId);
        }

        // Último intento: una imagen que se llame "cover".
        item ??= items.FirstOrDefault(e =>
            ((string?)e.Attribute("media-type"))?.StartsWith("image/") == true &&
            (((string?)e.Attribute("id"))?.Contains("cover", StringComparison.OrdinalIgnoreCase) == true ||
             ((string?)e.Attribute("href"))?.Contains("cover", StringComparison.OrdinalIgnoreCase) == true));

        return (string?)item?.Attribute("href");
    }

    // CBZ = ZIP de imágenes, con ComicInfo.xml opcional. La portada es la primera imagen en orden natural.
    // CBZ/CBR/CB7 = ZIP/RAR/7Z de imágenes. SharpCompress abre los tres.
    private static BookMetadata ReadComic(string path)
    {
        using IArchive archive = ArchiveFactory.OpenArchive(path, new ArchiveOptions());
        var files = archive.Entries.Where(e => !e.IsDirectory && e.Key is not null).ToList();
        IArchiveEntry? infoEntry = files.FirstOrDefault(e => Path.GetFileName(e.Key!).Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));
        XDocument? info = null;
        if (infoEntry is not null)
        {
            using Stream stream = infoEntry.OpenEntryStream();
            info = XDocument.Load(stream);
        }
        string? title = info?.Root?.Element("Title")?.Value;
        string? series = info?.Root?.Element("Series")?.Value;
        string? number = info?.Root?.Element("Number")?.Value;
        string? writer = info?.Root?.Element("Writer")?.Value;
        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(series))
            title = string.IsNullOrWhiteSpace(number) ? series : $"{series} #{number}";

        IArchiveEntry? first = files
            .Where(e => IsPageImage(e.Key!))
            .OrderBy(e => e.Key!, NaturalSort.Instance)
            .FirstOrDefault();
        byte[]? cover = null;
        if (first is not null)
        {
            using Stream stream = first.OpenEntryStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            cover = memory.ToArray();
        }
        return new BookMetadata(title?.Trim() ?? "", writer?.Trim(), series?.Trim(), cover);
    }

    /// <summary>¿Es un manga (se lee de derecha a izquierda)? Lo dice ComicInfo.xml si lo trae.</summary>
    public static bool IsManga(string path)
    {
        try
        {
            using IArchive archive = ArchiveFactory.OpenArchive(path, new ArchiveOptions());
            IArchiveEntry? entry = archive.Entries.FirstOrDefault(e => e.Key is not null && Path.GetFileName(e.Key).Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return false;
            using Stream stream = entry.OpenEntryStream();
            string? manga = XDocument.Load(stream).Root?.Element("Manga")?.Value;
            return manga is "Yes" or "YesAndRightToLeft";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) // archivo roto: se supone cómic normal
        {
            return false;
        }
    }

    /// <summary>Imagen de página: se ignoran carpetas de macOS y archivos ocultos.</summary>
    public static bool IsPageImage(string entryName)
    {
        string name = entryName.Replace('\\', '/');
        if (name.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(name).StartsWith('.'))
            return false;
        string extension = Path.GetExtension(name).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp";
    }

    private static string? FirstText(XElement? metadata, string localName) =>
        metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == localName && !string.IsNullOrWhiteSpace(e.Value))?.Value;

    // Las rutas del OPF son relativas a la carpeta del OPF y pueden venir con %20.
    private static string Combine(string opfPath, string href)
    {
        string folder = opfPath.Contains('/') ? opfPath[..(opfPath.LastIndexOf('/') + 1)] : "";
        var parts = new List<string>();
        foreach (string part in (folder + Uri.UnescapeDataString(href)).Split('/'))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part is not ("." or "")) parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private static XDocument? LoadXml(ZipArchive zip, string name)
    {
        byte[]? bytes = ReadEntry(zip, name);
        if (bytes is null)
            return null;
        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream);
    }

    private static byte[]? ReadEntry(ZipArchive zip, string name)
    {
        ZipArchiveEntry? entry = zip.GetEntry(name)
            ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : ReadAll(entry);
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
