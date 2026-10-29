using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Moon.Core.Library;

/// <summary>
/// La biblioteca en SQLite (<c>%APPDATA%\MOON\moon.db</c>): carpetas y libros.
/// Nunca toca los archivos de los libros; solo guarda dónde están y sus datos.
/// </summary>
public sealed class SqliteLibraryStore
{
    private const int SchemaVersion = 6;
    private readonly string _connectionString;

    public SqliteLibraryStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
        Migrate();
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MOON", "moon.db");

    private void Migrate()
    {
        using SqliteConnection connection = Open();
        long version = (long)Scalar(connection, "PRAGMA user_version")!;
        if (version >= SchemaVersion)
            return;

        // Cada versión añade lo suyo; una base de datos vieja pasa por todos los pasos que le falten.
        if (version < 1)
            CreateVersion1(connection);
        if (version < 2)
            Execute(connection, "ALTER TABLE books ADD COLUMN location TEXT"); // posición exacta (CFI) en el libro
        if (version < 3)
            Execute(connection, "ALTER TABLE books ADD COLUMN comic_view TEXT"); // dirección y ajustes de imagen (JSON)
        if (version < 4)
        {
            // Subrayados, marcadores y zonas de cómic. Se borran con su libro.
            Execute(connection, """
                CREATE TABLE annotations (
                    id          INTEGER PRIMARY KEY,
                    book_id     INTEGER NOT NULL REFERENCES books(id) ON DELETE CASCADE,
                    kind        TEXT NOT NULL,
                    location    TEXT NOT NULL,
                    text        TEXT NOT NULL DEFAULT '',
                    note        TEXT NOT NULL DEFAULT '',
                    color       TEXT NOT NULL,
                    chapter     TEXT NOT NULL DEFAULT '',
                    progress    REAL NOT NULL DEFAULT 0,
                    rect        TEXT,
                    created_at  TEXT NOT NULL
                );
                CREATE INDEX annotations_book ON annotations(book_id);
                """);
        }
        if (version < 5)
        {
            // Vocabulario: se conserva aunque el libro se quite de la biblioteca (book_id queda vacío).
            Execute(connection, """
                CREATE TABLE vocabulary (
                    id          INTEGER PRIMARY KEY,
                    word        TEXT NOT NULL,
                    lemma       TEXT NOT NULL,
                    translation TEXT NOT NULL,
                    context     TEXT NOT NULL DEFAULT '',
                    book_id     INTEGER REFERENCES books(id) ON DELETE SET NULL,
                    book_title  TEXT NOT NULL DEFAULT '',
                    location    TEXT,
                    created_at  TEXT NOT NULL
                );
                """);
        }
        if (version < 6)
        {
            // "Cosas por investigar": recortes con nota. Como el vocabulario, sobreviven al libro.
            Execute(connection, """
                CREATE TABLE research (
                    id          INTEGER PRIMARY KEY,
                    book_id     INTEGER REFERENCES books(id) ON DELETE SET NULL,
                    book_title  TEXT NOT NULL DEFAULT '',
                    location    TEXT,
                    place       TEXT NOT NULL DEFAULT '',
                    image_file  TEXT NOT NULL,
                    note        TEXT NOT NULL DEFAULT '',
                    created_at  TEXT NOT NULL
                );
                """);
        }
        Execute(connection, $"PRAGMA user_version = {SchemaVersion}");
    }

    private static void CreateVersion1(SqliteConnection connection)
    {
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS sources (
                id            INTEGER PRIMARY KEY,
                path          TEXT NOT NULL,
                kind          TEXT NOT NULL,
                volume_label  TEXT
            );
            CREATE TABLE IF NOT EXISTS books (
                id             INTEGER PRIMARY KEY,
                source_id      INTEGER NOT NULL REFERENCES sources(id) ON DELETE CASCADE,
                relative_path  TEXT NOT NULL,
                file_key       TEXT NOT NULL,
                format         TEXT NOT NULL,
                size           INTEGER NOT NULL,
                modified       TEXT NOT NULL,
                title          TEXT NOT NULL,
                author         TEXT,
                series         TEXT,
                has_cover      INTEGER NOT NULL DEFAULT 0,
                added_at       TEXT NOT NULL,
                last_opened_at TEXT,
                progress       REAL NOT NULL DEFAULT 0,
                UNIQUE (source_id, relative_path)
            );
            CREATE INDEX IF NOT EXISTS books_file_key ON books(file_key);
            """);
    }

    // ---- Carpetas ----

    public IReadOnlyList<LibrarySource> GetSources()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, path, kind, volume_label FROM sources ORDER BY id";
        using SqliteDataReader reader = command.ExecuteReader();
        var sources = new List<LibrarySource>();
        while (reader.Read())
            sources.Add(new LibrarySource(reader.GetInt64(0), reader.GetString(1), Enum.Parse<SourceKind>(reader.GetString(2)), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return sources;
    }

    public LibrarySource AddSource(string path, SourceKind kind, string? volumeLabel)
    {
        using SqliteConnection connection = Open();
        long id = (long)Scalar(connection, "INSERT INTO sources (path, kind, volume_label) VALUES ($path, $kind, $label) RETURNING id",
            ("$path", path), ("$kind", kind.ToString()), ("$label", volumeLabel))!;
        return new LibrarySource(id, path, kind, volumeLabel);
    }

    public void UpdateSourcePath(long id, string path)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE sources SET path = $path WHERE id = $id", ("$path", path), ("$id", id));
    }

    /// <summary>Quita la carpeta y sus libros de la biblioteca (los archivos no se borran).</summary>
    public void RemoveSource(long id)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "DELETE FROM sources WHERE id = $id", ("$id", id));
    }

    // ---- Libros ----

    public IReadOnlyList<Book> GetBooks()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, source_id, relative_path, file_key, format, size, modified, title, author, series,
                   has_cover, added_at, last_opened_at, progress, location, comic_view
            FROM books ORDER BY title COLLATE NOCASE
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var books = new List<Book>();
        while (reader.Read())
        {
            books.Add(new Book(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                Enum.Parse<BookFormat>(reader.GetString(4)), reader.GetInt64(5), ParseUtc(reader.GetString(6)),
                reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetInt64(10) != 0, ParseDate(reader.GetString(11))!.Value,
                reader.IsDBNull(12) ? null : ParseDate(reader.GetString(12)), reader.GetDouble(13))
            {
                Location = reader.IsDBNull(14) ? null : reader.GetString(14),
                ComicView = reader.IsDBNull(15) ? null : reader.GetString(15),
            });
        }
        return books;
    }

    public long AddBook(ScannedFile file, BookMetadata metadata, DateTime addedAt)
    {
        using SqliteConnection connection = Open();
        return (long)Scalar(connection, """
            INSERT INTO books (source_id, relative_path, file_key, format, size, modified, title, author, series, has_cover, added_at)
            VALUES ($source, $path, $key, $format, $size, $modified, $title, $author, $series, $cover, $added)
            RETURNING id
            """,
            ("$source", file.SourceId), ("$path", file.RelativePath), ("$key", file.FileKey), ("$format", file.Format.ToString()),
            ("$size", file.Size), ("$modified", FormatUtc(file.Modified)), ("$title", metadata.Title), ("$author", metadata.Author),
            ("$series", metadata.Series), ("$cover", metadata.Cover is null ? 0 : 1), ("$added", FormatDate(addedAt)))!;
    }

    /// <summary>El libro se movió o se renombró: se conservan su progreso y sus notas.</summary>
    public void MoveBook(long id, ScannedFile file)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE books SET source_id = $source, relative_path = $path, modified = $modified WHERE id = $id",
            ("$source", file.SourceId), ("$path", file.RelativePath), ("$modified", FormatUtc(file.Modified)), ("$id", id));
    }

    /// <summary>El archivo se reemplazó por otro en el mismo sitio: datos nuevos, mismo progreso.</summary>
    public void UpdateBookFile(long id, ScannedFile file, BookMetadata metadata)
    {
        using SqliteConnection connection = Open();
        Execute(connection, """
            UPDATE books SET file_key = $key, size = $size, modified = $modified, title = $title, author = $author,
                             series = $series, has_cover = $cover
            WHERE id = $id
            """,
            ("$key", file.FileKey), ("$size", file.Size), ("$modified", FormatUtc(file.Modified)), ("$title", metadata.Title),
            ("$author", metadata.Author), ("$series", metadata.Series), ("$cover", metadata.Cover is null ? 0 : 1), ("$id", id));
    }

    /// <summary>Mismo archivo con otra fecha (p. ej. se copió de nuevo): solo se apunta la fecha.</summary>
    public void TouchBook(long id, DateTime modified)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE books SET modified = $modified WHERE id = $id", ("$modified", FormatUtc(modified)), ("$id", id));
    }

    public void RemoveBook(long id)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "DELETE FROM books WHERE id = $id", ("$id", id));
    }

    /// <param name="location">Posición exacta para volver a ella (CFI en EPUB); null = no cambiarla.</param>
    public void SetProgress(long id, double progress, string? location, DateTime openedAt)
    {
        using SqliteConnection connection = Open();
        Execute(connection, """
            UPDATE books SET progress = $progress, location = COALESCE($location, location), last_opened_at = $opened
            WHERE id = $id
            """,
            ("$progress", Math.Clamp(progress, 0, 1)), ("$location", location), ("$opened", FormatDate(openedAt)), ("$id", id));
    }

    // ---- Ayudas ----

    /// <summary>Dirección de lectura y ajustes de imagen de un cómic (JSON de <see cref="Comics.ComicView"/>).</summary>
    public void SetComicView(long id, string json)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE books SET comic_view = $view WHERE id = $id", ("$view", json), ("$id", id));
    }

    // ---- Anotaciones ----

    public IReadOnlyList<Annotations.Annotation> GetAnnotations(long bookId)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, book_id, kind, location, text, note, color, chapter, progress, rect, created_at
            FROM annotations WHERE book_id = $book ORDER BY progress, created_at
            """;
        command.Parameters.AddWithValue("$book", bookId);
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<Annotations.Annotation>();
        while (reader.Read())
        {
            list.Add(new Annotations.Annotation(
                reader.GetInt64(0), reader.GetInt64(1), Enum.Parse<Annotations.AnnotationKind>(reader.GetString(2)),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), Enum.Parse<Annotations.HighlightColor>(reader.GetString(6)),
                reader.GetString(7), reader.GetDouble(8), ParseDate(reader.GetString(10))!.Value)
            {
                Rect = reader.IsDBNull(9) ? null : ParseRect(reader.GetString(9)),
            });
        }
        return list;
    }

    /// <returns>La anotación con su número asignado.</returns>
    public Annotations.Annotation AddAnnotation(Annotations.Annotation a)
    {
        using SqliteConnection connection = Open();
        long id = (long)Scalar(connection, """
            INSERT INTO annotations (book_id, kind, location, text, note, color, chapter, progress, rect, created_at)
            VALUES ($book, $kind, $location, $text, $note, $color, $chapter, $progress, $rect, $created)
            RETURNING id
            """,
            ("$book", a.BookId), ("$kind", a.Kind.ToString()), ("$location", a.Location), ("$text", a.Text), ("$note", a.Note),
            ("$color", a.Color.ToString()), ("$chapter", a.Chapter), ("$progress", a.Progress),
            ("$rect", a.Rect is { } r ? FormattableString.Invariant($"{r.X},{r.Y},{r.Width},{r.Height}") : null),
            ("$created", FormatDate(a.CreatedAt)))!;
        return a with { Id = id };
    }

    public void UpdateAnnotation(long id, string note, Annotations.HighlightColor color)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE annotations SET note = $note, color = $color WHERE id = $id",
            ("$note", note), ("$color", color.ToString()), ("$id", id));
    }

    public void DeleteAnnotation(long id)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "DELETE FROM annotations WHERE id = $id", ("$id", id));
    }

    // ---- Vocabulario ----

    public IReadOnlyList<Dictionary.VocabularyEntry> GetVocabulary()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, word, lemma, translation, context, book_id, book_title, location, created_at
            FROM vocabulary ORDER BY created_at DESC
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<Dictionary.VocabularyEntry>();
        while (reader.Read())
        {
            list.Add(new Dictionary.VocabularyEntry(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), ParseDate(reader.GetString(8))!.Value));
        }
        return list;
    }

    /// <summary>Guarda la palabra; si ya estaba (misma forma base), no la repite y devuelve la existente.</summary>
    public Dictionary.VocabularyEntry AddVocabulary(Dictionary.VocabularyEntry entry)
    {
        using SqliteConnection connection = Open();
        if (Scalar(connection, "SELECT id FROM vocabulary WHERE lemma = $lemma LIMIT 1", ("$lemma", entry.Lemma)) is long existing)
            return GetVocabulary().First(v => v.Id == existing);
        long id = (long)Scalar(connection, """
            INSERT INTO vocabulary (word, lemma, translation, context, book_id, book_title, location, created_at)
            VALUES ($word, $lemma, $translation, $context, $book, $title, $location, $created)
            RETURNING id
            """,
            ("$word", entry.Word), ("$lemma", entry.Lemma), ("$translation", entry.Translation), ("$context", entry.Context),
            ("$book", entry.BookId), ("$title", entry.BookTitle), ("$location", entry.Location), ("$created", FormatDate(entry.CreatedAt)))!;
        return entry with { Id = id };
    }

    public void DeleteVocabulary(long id)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "DELETE FROM vocabulary WHERE id = $id", ("$id", id));
    }

    // ---- Cosas por investigar ----

    public IReadOnlyList<Research.ResearchItem> GetResearch()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, book_id, book_title, location, place, image_file, note, created_at
            FROM research ORDER BY created_at DESC
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var list = new List<Research.ResearchItem>();
        while (reader.Read())
        {
            list.Add(new Research.ResearchItem(
                reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetString(6), ParseDate(reader.GetString(7))!.Value));
        }
        return list;
    }

    public Research.ResearchItem AddResearch(Research.ResearchItem item)
    {
        using SqliteConnection connection = Open();
        long id = (long)Scalar(connection, """
            INSERT INTO research (book_id, book_title, location, place, image_file, note, created_at)
            VALUES ($book, $title, $location, $place, $image, $note, $created)
            RETURNING id
            """,
            ("$book", item.BookId), ("$title", item.BookTitle), ("$location", item.Location), ("$place", item.Where),
            ("$image", item.ImageFile), ("$note", item.Note), ("$created", FormatDate(item.CreatedAt)))!;
        return item with { Id = id };
    }

    public void UpdateResearchNote(long id, string note)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE research SET note = $note WHERE id = $id", ("$note", note), ("$id", id));
    }

    public void DeleteResearch(long id)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "DELETE FROM research WHERE id = $id", ("$id", id));
    }

    private static Annotations.NormalizedRect? ParseRect(string text)
    {
        double[] parts = [.. text.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture))];
        return parts.Length == 4 ? new Annotations.NormalizedRect(parts[0], parts[1], parts[2], parts[3]) : null;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using SqliteCommand command = Prepare(connection, sql, parameters);
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using SqliteCommand command = Prepare(connection, sql, parameters);
        return command.ExecuteScalar();
    }

    private static SqliteCommand Prepare(SqliteConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private static string FormatDate(DateTime date) => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime? ParseDate(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();

    // La fecha de modificación del archivo se compara tal cual: siempre en UTC, sin conversiones.
    private static string FormatUtc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
