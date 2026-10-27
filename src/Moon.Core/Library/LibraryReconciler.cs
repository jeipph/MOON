namespace Moon.Core.Library;

/// <summary>Qué hay que cambiar en la biblioteca tras recorrer las carpetas.</summary>
public sealed record LibraryChanges(
    IReadOnlyList<ScannedFile> Added,
    IReadOnlyList<(Book Book, ScannedFile File)> Moved,
    IReadOnlyList<(Book Book, ScannedFile File)> Changed,
    IReadOnlyList<Book> Removed)
{
    public bool IsEmpty => Added.Count == 0 && Moved.Count == 0 && Changed.Count == 0 && Removed.Count == 0;
}

/// <summary>
/// Compara lo guardado con lo encontrado en disco. Solo debe recibir libros de carpetas
/// que estén conectadas: los de un disco desconectado no se tocan.
/// </summary>
public static class LibraryReconciler
{
    public static LibraryChanges Compare(IReadOnlyCollection<Book> existing, IReadOnlyCollection<ScannedFile> scanned)
    {
        var scannedByPath = scanned.ToDictionary(f => (f.SourceId, Normalize(f.RelativePath)));
        var existingPaths = existing.Select(b => (b.SourceId, Normalize(b.RelativePath))).ToHashSet();

        var changed = new List<(Book, ScannedFile)>();
        var missing = new List<Book>();
        foreach (Book book in existing)
        {
            if (scannedByPath.TryGetValue((book.SourceId, Normalize(book.RelativePath)), out ScannedFile? file))
            {
                // Mismo sitio pero otro contenido (se reemplazó el archivo): volver a leer sus datos.
                if (file.FileKey != book.FileKey)
                    changed.Add((book, file));
            }
            else
            {
                missing.Add(book);
            }
        }

        // Archivos nuevos en sitios donde no había ningún libro.
        var fresh = scanned.Where(f => !existingPaths.Contains((f.SourceId, Normalize(f.RelativePath)))).ToList();

        // Un libro que falta y un archivo nuevo con la misma huella = el libro se movió o se renombró.
        var moved = new List<(Book, ScannedFile)>();
        var removed = new List<Book>();
        foreach (Book book in missing)
        {
            int index = fresh.FindIndex(f => f.FileKey == book.FileKey);
            if (index >= 0)
            {
                moved.Add((book, fresh[index]));
                fresh.RemoveAt(index);
            }
            else
            {
                removed.Add(book);
            }
        }

        return new LibraryChanges(fresh, moved, changed, removed);
    }

    // Windows no distingue mayúsculas en las rutas.
    private static string Normalize(string relativePath) => relativePath.Replace('/', '\\').ToUpperInvariant();
}
