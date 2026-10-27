using Microsoft.Data.Sqlite;
using Moon.Core.Library;

namespace Moon.Core.Tests.Library;

/// <summary>Pruebas de la biblioteca completa con carpetas y base de datos reales (temporales).</summary>
public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MOONTests", Guid.NewGuid().ToString("N"));
    private readonly List<DriveSnapshot> _drives = [];

    private string Managed => Path.Combine(_root, "Biblioteca");
    private string Covers => Path.Combine(_root, "covers");
    private string External => Path.Combine(_root, "DiscoExterno", "Libros");

    private LibraryService NewService() =>
        new(new SqliteLibraryStore(Path.Combine(_root, "moon.db")), Managed, Covers, () => _drives);

    [Fact]
    public void Import_CopiesBooks_SkipsDuplicatesAndNonBooks()
    {
        string downloads = Path.Combine(_root, "Descargas");
        string epub = TestBooks.Epub(downloads, "viento.epub");
        string same = Path.Combine(downloads, "copia del viento.epub");
        File.Copy(epub, same);
        string photo = TestBooks.Text(downloads, "foto.jpg", "no");
        LibraryService library = NewService();

        ImportResult result = library.Import([epub, same, photo]);

        Assert.Equal(new ImportResult(Added: 1, AlreadyInLibrary: 1, NotBooks: 1), result);
        Assert.True(File.Exists(Path.Combine(Managed, "viento.epub")));
        Assert.True(File.Exists(epub)); // el original no se toca
        Book book = Assert.Single(library.Books);
        Assert.Equal("El nombre del viento", book.Title);
        Assert.True(book.HasCover);
        Assert.True(File.Exists(library.CoverPath(book.Id)));
    }

    [Fact]
    public void Link_ReadsBooksInPlace_IncludingSubfolders()
    {
        TestBooks.Epub(External, "uno.epub");
        TestBooks.Cbz(Path.Combine(External, "Cómics"), "saga.cbz");
        TestBooks.Text(External, "léeme.docx", "no es un libro");
        LibraryService library = NewService();

        Assert.Equal(LinkResult.Linked, library.Link(External));

        Assert.Equal(2, library.Books.Count);
        Assert.Contains(library.Books, b => b.Title == "Saga #3");
        Assert.All(library.Books, b => Assert.NotNull(library.FullPath(b)));
    }

    [Fact]
    public void Link_RejectsSameFolderOrFolderInside()
    {
        Directory.CreateDirectory(Path.Combine(External, "Sub"));
        LibraryService library = NewService();
        library.Link(External);

        Assert.Equal(LinkResult.AlreadyLinked, library.Link(External));
        Assert.Equal(LinkResult.AlreadyLinked, library.Link(Path.Combine(External, "Sub")));
        Assert.Equal(LinkResult.ContainsLinkedFolder, library.Link(Path.GetDirectoryName(External)!));
        Assert.Equal(LinkResult.NotFound, library.Link(Path.Combine(_root, "no existe")));
    }

    [Fact]
    public void RenamedBook_KeepsItsProgress()
    {
        string path = TestBooks.Epub(External, "uno.epub");
        LibraryService library = NewService();
        library.Link(External);
        Book before = Assert.Single(library.Books);
        library.SetProgress(before.Id, 0.42);

        Directory.CreateDirectory(Path.Combine(External, "Leídos"));
        File.Move(path, Path.Combine(External, "Leídos", "renombrado.epub"));
        SyncResult result = library.Sync();

        Book after = Assert.Single(library.Books);
        Assert.Equal(1, result.Moved);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(0.42, after.Progress, precision: 3);
        Assert.Equal(Path.Combine("Leídos", "renombrado.epub"), after.RelativePath);
    }

    [Fact]
    public void DisconnectedDisk_KeepsBooks_AndTheyComeBack()
    {
        TestBooks.Epub(External, "uno.epub");
        LibraryService library = NewService();
        library.Link(External);
        string hidden = External + "-desconectado";

        Directory.Move(External, hidden); // "se desenchufa el disco"
        SyncResult offline = library.Sync();
        Book book = Assert.Single(library.Books);
        Assert.Equal(1, offline.DisconnectedSources);
        Assert.Equal(0, offline.Removed);
        Assert.Null(library.FullPath(book));

        Directory.Move(hidden, External); // "se vuelve a enchufar"
        library.Sync();
        Assert.NotNull(library.FullPath(Assert.Single(library.Books)));
    }

    [Fact]
    public void UnchangedFiles_AreNotReadAgain()
    {
        string path = TestBooks.Text(External, "notas.txt", "contenido original");
        LibraryService library = NewService();
        library.Link(External);
        string keyBefore = Assert.Single(library.Books).FileKey;

        // Otro contenido, pero mismo tamaño y misma fecha: MOON confía en la fecha y no relee el archivo.
        DateTime modified = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "contenido cambiado");
        File.SetLastWriteTimeUtc(path, modified);
        library.Sync();
        Assert.Equal(keyBefore, Assert.Single(library.Books).FileKey);

        // Con otra fecha sí lo relee.
        File.SetLastWriteTimeUtc(path, modified.AddMinutes(1));
        library.Sync();
        Assert.NotEqual(keyBefore, Assert.Single(library.Books).FileKey);
    }

    [Fact]
    public void DeletedFile_LeavesTheLibrary()
    {
        string path = TestBooks.Epub(External, "uno.epub");
        LibraryService library = NewService();
        library.Link(External);

        File.Delete(path);
        library.Sync();

        Assert.Empty(library.Books);
    }

    [Fact]
    public void Unlink_RemovesBooks_ButNotFiles()
    {
        string path = TestBooks.Epub(External, "uno.epub");
        LibraryService library = NewService();
        library.Link(External);

        library.Unlink(library.Sources.Single(s => s.Kind == SourceKind.Linked).Id);

        Assert.Empty(library.Books);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Library_SurvivesRestart()
    {
        TestBooks.Epub(External, "uno.epub");
        NewService().Link(External);

        LibraryService reopened = NewService();
        reopened.Sync();

        Assert.Single(reopened.Books);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools(); // SQLite deja el archivo abierto en su caché de conexiones.
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
