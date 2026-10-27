using System.IO.Compression;
using System.Xml.Linq;

namespace Moon.Core.Library;

/// <summary>Título, autor y portada sacados del propio archivo (sin internet).</summary>
public sealed record BookMetadata(string Title, string? Author, string? Series, byte[]? Cover)
{
    /// <summary>Lee lo que se pueda; si el archivo está dañado, al menos devuelve el nombre del archivo como título.</summary>
    public static BookMetadata Read(string path, BookFormat format)
    {
        BookMetadata fallback = FromFileName(path);
        try
        {
            BookMetadata? read = format switch
            {
                BookFormat.Epub => ReadEpub(path),
                BookFormat.Cbz => ReadComicZip(path),
                _ => null,
            };
            if (read is null)
                return fallback;
            return read with { Title = string.IsNullOrWhiteSpace(read.Title) ? fallback.Title : read.Title };
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Xml.XmlException or UnauthorizedAccessException)
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
    private static BookMetadata ReadComicZip(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);
        XDocument? info = LoadXml(zip, "ComicInfo.xml");
        string? title = info?.Root?.Element("Title")?.Value;
        string? series = info?.Root?.Element("Series")?.Value;
        string? number = info?.Root?.Element("Number")?.Value;
        string? writer = info?.Root?.Element("Writer")?.Value;
        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(series))
            title = string.IsNullOrWhiteSpace(number) ? series : $"{series} #{number}";

        ZipArchiveEntry? first = zip.Entries
            .Where(e => IsPageImage(e.FullName))
            .OrderBy(e => e.FullName, NaturalSort.Instance)
            .FirstOrDefault();
        byte[]? cover = first is null ? null : ReadAll(first);
        return new BookMetadata(title?.Trim() ?? "", writer?.Trim(), series?.Trim(), cover);
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
