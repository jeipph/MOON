using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Moon.Core.Library;

/// <summary>
/// La biblioteca en SQLite (<c>%APPDATA%\MOON\moon.db</c>): carpetas y libros.
/// Nunca toca los archivos de los libros; solo guarda dónde están y sus datos.
/// </summary>
public sealed class SqliteLibraryStore
{
    private const int SchemaVersion = 1;
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
        Execute(connection, $"PRAGMA user_version = {SchemaVersion}");
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
                   has_cover, added_at, last_opened_at, progress
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
                reader.IsDBNull(12) ? null : ParseDate(reader.GetString(12)), reader.GetDouble(13)));
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

    public void SetProgress(long id, double progress, DateTime openedAt)
    {
        using SqliteConnection connection = Open();
        Execute(connection, "UPDATE books SET progress = $progress, last_opened_at = $opened WHERE id = $id",
            ("$progress", Math.Clamp(progress, 0, 1)), ("$opened", FormatDate(openedAt)), ("$id", id));
    }

    // ---- Ayudas ----

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
