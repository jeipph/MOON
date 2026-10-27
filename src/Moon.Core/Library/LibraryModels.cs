namespace Moon.Core.Library;

public enum SourceKind
{
    /// <summary>La carpeta propia de MOON: "Añadir libros…" copia los archivos aquí.</summary>
    Managed,

    /// <summary>Carpeta enlazada (p. ej. un disco externo): los archivos se quedan donde están.</summary>
    Linked,
}

/// <summary>Una carpeta de la biblioteca.</summary>
/// <param name="VolumeLabel">Nombre del disco; sirve para encontrarlo si Windows le cambia la letra.</param>
public sealed record LibrarySource(long Id, string Path, SourceKind Kind, string? VolumeLabel);

/// <summary>Un libro de la biblioteca. La ruta se guarda relativa a su carpeta.</summary>
public sealed record Book(
    long Id,
    long SourceId,
    string RelativePath,
    string FileKey,
    BookFormat Format,
    long Size,
    DateTime Modified,
    string Title,
    string? Author,
    string? Series,
    bool HasCover,
    DateTime AddedAt,
    DateTime? LastOpenedAt,
    double Progress)
{
    /// <summary>Dónde se quedó la lectura (CFI en EPUB). Null = nunca abierto.</summary>
    public string? Location { get; init; }

    /// <summary>Formatos que el lector de libros ya sabe abrir.</summary>
    public bool CanBeRead => Format is BookFormat.Epub or BookFormat.Mobi or BookFormat.Azw3 or BookFormat.Fb2 or BookFormat.Cbz;
}

/// <summary>Un archivo encontrado al recorrer una carpeta. <paramref name="Modified"/> en UTC.</summary>
public sealed record ScannedFile(long SourceId, string RelativePath, string FileKey, BookFormat Format, long Size, DateTime Modified);
