using System.IO.Compression;
using Moon.Core.Library;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Moon.Core.Comics;

/// <summary>
/// Las páginas de un cómic, sea un CBZ, un CBR, un CB7 o una carpeta de imágenes.
/// Nunca se descomprime todo: cada página se lee cuando hace falta.
/// </summary>
public interface IPageSource : IDisposable
{
    int Count { get; }

    /// <summary>Nombre de la página (por ejemplo "001.jpg").</summary>
    string Name(int index);

    /// <summary>
    /// Los bytes de la imagen de la página.
    /// <paramref name="targetWidth"/> solo lo usan las fuentes que dibujan la página (PDF).
    /// </summary>
    byte[] Read(int index, int targetWidth);
}

/// <summary>Abre archivos de cómic (ZIP, RAR, 7Z) y carpetas de imágenes.</summary>
public static class PageSources
{
    /// <exception cref="InvalidDataException">Archivo dañado, cifrado o sin imágenes.</exception>
    public static IPageSource OpenArchive(string path)
    {
        if (Directory.Exists(path))
            return new FolderPageSource(path);
        return IsZip(path) ? new ZipPageSource(path) : new SharpCompressPageSource(path);
    }

    // Muchos ".cbr" son en realidad ZIP y algunos ".cbz" son RAR: se mira el contenido, no la extensión.
    private static bool IsZip(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 4 && header[0] == 'P' && header[1] == 'K';
    }

    /// <summary>Páginas en orden natural, sin archivos ocultos ni carpetas de macOS.</summary>
    internal static List<T> SortPages<T>(IEnumerable<T> entries, Func<T, string> name) =>
        [.. entries.Where(e => BookMetadata.IsPageImage(name(e))).OrderBy(name, NaturalSort.Instance)];
}

internal sealed class ZipPageSource : IPageSource
{
    private readonly ZipArchive _zip;
    private readonly List<ZipArchiveEntry> _pages;
    private readonly Lock _lock = new();

    public ZipPageSource(string path)
    {
        _zip = ZipFile.OpenRead(path);
        _pages = PageSources.SortPages(_zip.Entries, e => e.FullName);
        if (_pages.Count == 0)
        {
            _zip.Dispose(); // si no, el archivo queda bloqueado
            throw new InvalidDataException("El archivo no tiene imágenes.");
        }
    }

    public int Count => _pages.Count;

    public string Name(int index) => _pages[index].FullName;

    public byte[] Read(int index, int targetWidth)
    {
        lock (_lock) // ZipArchive no admite dos lecturas a la vez
        {
            using Stream stream = _pages[index].Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    public void Dispose() => _zip.Dispose();
}

internal sealed class SharpCompressPageSource : IPageSource
{
    private readonly IArchive _archive;
    private readonly List<IArchiveEntry> _pages;
    private readonly Lock _lock = new();

    public SharpCompressPageSource(string path)
    {
        try
        {
            _archive = ArchiveFactory.OpenArchive(path, new ReaderOptions());
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException("No se pudo abrir el archivo (¿dañado o en un formato desconocido?).", exception);
        }
        if (_archive.IsEncrypted || _archive.Entries.Any(e => e.IsEncrypted))
        {
            _archive.Dispose();
            throw new InvalidDataException("El archivo está protegido con contraseña.");
        }
        _pages = PageSources.SortPages(_archive.Entries.Where(e => !e.IsDirectory), e => e.Key ?? "");
        if (_pages.Count == 0)
        {
            _archive.Dispose();
            throw new InvalidDataException("El archivo no tiene imágenes.");
        }
    }

    public int Count => _pages.Count;

    public string Name(int index) => _pages[index].Key ?? "";

    public byte[] Read(int index, int targetWidth)
    {
        lock (_lock)
        {
            using Stream stream = _pages[index].OpenEntryStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    public void Dispose() => _archive.Dispose();
}

internal sealed class FolderPageSource : IPageSource
{
    private readonly List<string> _pages;

    public FolderPageSource(string folder)
    {
        _pages = PageSources.SortPages(Directory.EnumerateFiles(folder), Path.GetFileName);
        _pages = [.. _pages.Select(name => Path.Combine(folder, name))];
        if (_pages.Count == 0)
            throw new InvalidDataException("La carpeta no tiene imágenes.");
    }

    public int Count => _pages.Count;

    public string Name(int index) => Path.GetFileName(_pages[index]);

    public byte[] Read(int index, int targetWidth) => File.ReadAllBytes(_pages[index]);

    public void Dispose() { }
}
