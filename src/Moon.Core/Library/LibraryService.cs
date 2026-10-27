namespace Moon.Core.Library;

public enum LinkResult
{
    Linked,
    AlreadyLinked,
    ContainsLinkedFolder,
    NotFound,
}

public sealed record ImportResult(int Added, int AlreadyInLibrary, int NotBooks);

public sealed record SyncResult(int Added, int Moved, int Removed, int DisconnectedSources);

/// <summary>
/// La biblioteca de MOON: carpeta propia ("Añadir libros…") y carpetas enlazadas (discos externos).
/// Recorre las carpetas, reconoce libros movidos y guarda las portadas en caché.
/// Todo es lento (disco), así que la interfaz lo llama en segundo plano.
/// </summary>
public sealed class LibraryService
{
    private readonly SqliteLibraryStore _store;
    private readonly string _managedFolder;
    private readonly string _coversFolder;
    private readonly Func<IEnumerable<DriveSnapshot>> _drives;
    private readonly Func<string, BookFormat, BookMetadata?>? _platformReader;
    private readonly Lock _lock = new();
    private Dictionary<long, string?> _currentPaths = [];

    /// <param name="platformReader">Lee portada y datos de formatos que necesitan Windows (PDF).</param>
    public LibraryService(SqliteLibraryStore store, string managedFolder, string coversFolder, Func<IEnumerable<DriveSnapshot>> drives,
        Func<string, BookFormat, BookMetadata?>? platformReader = null)
    {
        _store = store;
        _managedFolder = managedFolder;
        _coversFolder = coversFolder;
        _drives = drives;
        _platformReader = platformReader;
    }

    public static string DefaultManagedFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MOON", "Biblioteca");

    public static string DefaultCoversFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOON", "covers");

    /// <summary>Unidades conectadas ahora mismo (letra y nombre del disco).</summary>
    public static IEnumerable<DriveSnapshot> CurrentDrives()
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            string? label = null;
            try
            {
                if (drive.IsReady)
                    label = drive.VolumeLabel;
            }
            catch (IOException)
            {
                continue; // Lector de tarjetas vacío, unidad de red caída…
            }
            yield return new DriveSnapshot(drive.Name, label);
        }
    }

    public IReadOnlyList<LibrarySource> Sources => _store.GetSources();

    public IReadOnlyList<Book> Books => _store.GetBooks();

    /// <summary>Ruta actual de cada carpeta; null = disco desconectado. Se actualiza en <see cref="Sync"/>.</summary>
    public IReadOnlyDictionary<long, string?> CurrentPaths
    {
        get { lock (_lock) return new Dictionary<long, string?>(_currentPaths); }
    }

    public string CoverPath(long bookId) => Path.Combine(_coversFolder, $"{bookId}.img");

    /// <summary>Ruta completa del archivo, o null si su disco no está conectado.</summary>
    public string? FullPath(Book book)
    {
        string? folder = CurrentPaths.GetValueOrDefault(book.SourceId);
        return folder is null ? null : Path.Combine(folder, book.RelativePath);
    }

    /// <summary>"Añadir libros…": copia los archivos a la carpeta de MOON.</summary>
    public ImportResult Import(IEnumerable<string> files, IProgress<string>? progress = null)
    {
        LibrarySource managed = EnsureManagedSource();
        Directory.CreateDirectory(_managedFolder);
        var knownKeys = _store.GetBooks().Select(b => b.FileKey).ToHashSet();

        int added = 0, duplicates = 0, notBooks = 0;
        foreach (string file in files)
        {
            if (BookFormats.FromPath(file) is null || !File.Exists(file))
            {
                notBooks++;
                continue;
            }
            string key = FileKey.Compute(file);
            if (!knownKeys.Add(key))
            {
                duplicates++;
                continue;
            }
            progress?.Report($"Copiando {Path.GetFileName(file)}…");
            File.Copy(file, FreeName(_managedFolder, Path.GetFileName(file)));
            added++;
        }
        if (added > 0)
            Sync(progress, onlySource: managed.Id);
        return new ImportResult(added, duplicates, notBooks);
    }

    /// <summary>"Enlazar carpeta…": los libros se quedan donde están (p. ej. un disco externo).</summary>
    public LinkResult Link(string folder, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(folder))
            return LinkResult.NotFound;
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

        foreach (LibrarySource source in _store.GetSources())
        {
            string existing = Path.TrimEndingDirectorySeparator(CurrentPaths.GetValueOrDefault(source.Id) ?? source.Path);
            if (IsSameOrInside(full, existing))
                return LinkResult.AlreadyLinked;
            if (IsSameOrInside(existing, full))
                return LinkResult.ContainsLinkedFolder;
        }

        string root = Path.GetPathRoot(full) ?? "";
        string? label = _drives().FirstOrDefault(d => string.Equals(d.Root, root, StringComparison.OrdinalIgnoreCase))?.Label;
        LibrarySource added = _store.AddSource(full, SourceKind.Linked, string.IsNullOrWhiteSpace(label) ? null : label);
        Sync(progress, onlySource: added.Id);
        return LinkResult.Linked;
    }

    /// <summary>Quita una carpeta enlazada de la biblioteca. Los archivos no se tocan.</summary>
    public void Unlink(long sourceId)
    {
        foreach (Book book in _store.GetBooks().Where(b => b.SourceId == sourceId))
            DeleteCover(book.Id);
        _store.RemoveSource(sourceId);
        lock (_lock) _currentPaths.Remove(sourceId);
    }

    /// <summary>Quita un libro de la biblioteca. Si está en la carpeta de MOON, también borra la copia.</summary>
    public void Remove(Book book)
    {
        LibrarySource? source = _store.GetSources().FirstOrDefault(s => s.Id == book.SourceId);
        if (source?.Kind == SourceKind.Managed && FullPath(book) is { } path && File.Exists(path))
            File.Delete(path);
        DeleteCover(book.Id);
        _store.RemoveBook(book.Id);
    }

    /// <summary>Guarda por dónde va la lectura (y marca el libro como abierto ahora).</summary>
    public void SetProgress(long bookId, double progress, string? location = null) =>
        _store.SetProgress(bookId, progress, location, DateTime.Now);

    // ---- Vocabulario ----

    public IReadOnlyList<Dictionary.VocabularyEntry> Vocabulary => _store.GetVocabulary();

    public Dictionary.VocabularyEntry AddVocabulary(Dictionary.VocabularyEntry entry) => _store.AddVocabulary(entry);

    public void DeleteVocabulary(long id) => _store.DeleteVocabulary(id);

    // ---- Subrayados, marcadores y zonas ----

    public IReadOnlyList<Annotations.Annotation> GetAnnotations(long bookId) => _store.GetAnnotations(bookId);

    public Annotations.Annotation AddAnnotation(Annotations.Annotation annotation) => _store.AddAnnotation(annotation);

    public void UpdateAnnotation(long id, string note, Annotations.HighlightColor color) => _store.UpdateAnnotation(id, note, color);

    public void DeleteAnnotation(long id) => _store.DeleteAnnotation(id);

    /// <summary>Recuerda la dirección de lectura y los ajustes de imagen de un cómic.</summary>
    public void SetComicView(long bookId, Comics.ComicView view) => _store.SetComicView(bookId, view.ToJson());

    /// <summary>"Continuar leyendo": abiertos y sin terminar, el último primero.</summary>
    public static IEnumerable<Book> ContinueReading(IEnumerable<Book> books) =>
        books.Where(b => b.LastOpenedAt is not null && b.Progress < 0.995)
             .OrderByDescending(b => b.LastOpenedAt);

    /// <summary>Recorre las carpetas conectadas y pone la biblioteca al día.</summary>
    public SyncResult Sync(IProgress<string>? progress = null, long? onlySource = null)
    {
        EnsureManagedSource();
        var drives = _drives().ToList();
        var paths = new Dictionary<long, string?>();
        foreach (LibrarySource source in _store.GetSources())
        {
            string? path = source.Kind == SourceKind.Managed
                ? _managedFolder
                : SourceLocator.Locate(source, drives, Directory.Exists);
            if (path is not null && source.Kind == SourceKind.Linked && !string.Equals(path, source.Path, StringComparison.OrdinalIgnoreCase))
                _store.UpdateSourcePath(source.Id, path); // El disco cambió de letra.
            paths[source.Id] = path;
        }
        lock (_lock) _currentPaths = paths;

        var reachable = paths.Where(p => p.Value is not null && (onlySource is null || p.Key == onlySource)).ToList();
        var reachableIds = reachable.Select(p => p.Key).ToHashSet();
        var existing = _store.GetBooks().Where(b => reachableIds.Contains(b.SourceId)).ToList();
        var known = existing.ToDictionary(b => (b.SourceId, b.RelativePath.ToUpperInvariant()));

        var scanned = new List<ScannedFile>();
        foreach (var (sourceId, folder) in reachable)
            scanned.AddRange(Scan(sourceId, folder!, known, progress));

        LibraryChanges changes = LibraryReconciler.Compare(existing, scanned);

        // Mismo contenido con otra fecha: se apunta para no volver a leerlo la próxima vez.
        foreach (ScannedFile file in scanned)
        {
            if (known.TryGetValue((file.SourceId, file.RelativePath.ToUpperInvariant()), out Book? book) &&
                book.FileKey == file.FileKey && book.Modified != file.Modified)
                _store.TouchBook(book.Id, file.Modified);
        }

        int done = 0;
        foreach (ScannedFile file in changes.Added)
        {
            progress?.Report($"Leyendo libros… {++done} de {changes.Added.Count}");
            BookMetadata metadata = BookMetadata.Read(Path.Combine(paths[file.SourceId]!, file.RelativePath), file.Format, _platformReader);
            long id = _store.AddBook(file, metadata, DateTime.Now);
            SaveCover(id, metadata.Cover);
        }
        foreach (var (book, file) in changes.Moved)
            _store.MoveBook(book.Id, file);
        foreach (var (book, file) in changes.Changed)
        {
            BookMetadata metadata = BookMetadata.Read(Path.Combine(paths[file.SourceId]!, file.RelativePath), file.Format, _platformReader);
            _store.UpdateBookFile(book.Id, file, metadata);
            SaveCover(book.Id, metadata.Cover);
        }
        foreach (Book book in changes.Removed)
        {
            DeleteCover(book.Id);
            _store.RemoveBook(book.Id);
        }

        int disconnected = paths.Count(p => p.Value is null);
        return new SyncResult(changes.Added.Count, changes.Moved.Count, changes.Removed.Count, disconnected);
    }

    private static IEnumerable<ScannedFile> Scan(long sourceId, string folder, Dictionary<(long, string), Book> known, IProgress<string>? progress)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        int count = 0;
        foreach (string path in Directory.EnumerateFiles(folder, "*", options))
        {
            if (BookFormats.FromPath(path) is not { } format)
                continue;
            ScannedFile? file = null;
            try
            {
                var info = new FileInfo(path);
                string relative = Path.GetRelativePath(folder, path);
                DateTime modified = info.LastWriteTimeUtc;
                // Si el tamaño y la fecha no cambiaron, la huella tampoco: no hace falta leer el archivo.
                string key = known.TryGetValue((sourceId, relative.ToUpperInvariant()), out Book? book) &&
                             book.Size == info.Length && book.Modified == modified
                    ? book.FileKey
                    : FileKey.Compute(path);
                file = new ScannedFile(sourceId, relative, key, format, info.Length, modified);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Archivo en uso o sin permiso: se intentará en la próxima vuelta.
            }
            if (file is not null)
            {
                if (++count % 25 == 0)
                    progress?.Report($"Buscando libros en {folder}… {count}");
                yield return file;
            }
        }
    }

    private LibrarySource EnsureManagedSource()
    {
        LibrarySource? managed = _store.GetSources().FirstOrDefault(s => s.Kind == SourceKind.Managed);
        if (managed is not null)
            return managed;
        Directory.CreateDirectory(_managedFolder);
        return _store.AddSource(_managedFolder, SourceKind.Managed, null);
    }

    private void SaveCover(long bookId, byte[]? cover)
    {
        if (cover is null)
        {
            DeleteCover(bookId);
            return;
        }
        Directory.CreateDirectory(_coversFolder);
        File.WriteAllBytes(CoverPath(bookId), cover);
    }

    private void DeleteCover(long bookId)
    {
        string path = CoverPath(bookId);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>"libro.epub" ya existe → "libro (2).epub".</summary>
    private static string FreeName(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        for (int i = 2; File.Exists(candidate); i++)
            candidate = Path.Combine(folder, $"{name} ({i}){extension}");
        return candidate;
    }

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
