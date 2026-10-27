namespace Moon.Core.Library;

public enum BookFormat
{
    Epub,
    Pdf,
    Cbz,
    Cbr,
    Cb7,
    Mobi,
    Azw3,
    Fb2,
    Txt,
}

public static class BookFormats
{
    private static readonly Dictionary<string, BookFormat> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".epub"] = BookFormat.Epub,
        [".pdf"] = BookFormat.Pdf,
        [".cbz"] = BookFormat.Cbz,
        [".cbr"] = BookFormat.Cbr,
        [".cb7"] = BookFormat.Cb7,
        [".mobi"] = BookFormat.Mobi,
        [".azw3"] = BookFormat.Azw3,
        [".fb2"] = BookFormat.Fb2,
        [".txt"] = BookFormat.Txt,
    };

    /// <summary>El formato según la extensión, o null si no es un libro (imágenes sueltas, .docx…).</summary>
    public static BookFormat? FromPath(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out var format) ? format : null;

    /// <summary>Etiqueta corta para mostrar en la portada ("EPUB", "CBZ"…).</summary>
    public static string Label(BookFormat format) => format.ToString().ToUpperInvariant();

    /// <summary>Cómics (archivos de imágenes) frente a libros de texto.</summary>
    public static bool IsComic(BookFormat format) => format is BookFormat.Cbz or BookFormat.Cbr or BookFormat.Cb7;
}
