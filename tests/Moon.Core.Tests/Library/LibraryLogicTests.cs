using Moon.Core.Library;

namespace Moon.Core.Tests.Library;

public sealed class LibraryLogicTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MOONTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("libro.EPUB", BookFormat.Epub)]
    [InlineData("comic.cbz", BookFormat.Cbz)]
    [InlineData("comic.cbr", BookFormat.Cbr)]
    [InlineData("doc.pdf", BookFormat.Pdf)]
    [InlineData("novela.azw3", BookFormat.Azw3)]
    public void Formats_AreRecognized(string file, BookFormat expected) =>
        Assert.Equal(expected, BookFormats.FromPath(file));

    [Theory]
    [InlineData("foto.jpg")]
    [InlineData("notas.docx")]
    [InlineData("sin_extension")]
    public void OtherFiles_AreNotBooks(string file) => Assert.Null(BookFormats.FromPath(file));

    [Fact]
    public void NaturalSort_PutsPage2BeforePage10()
    {
        string[] pages = ["Página 10", "página 2", "Página 1", "Página 002b"];
        Assert.Equal(["Página 1", "página 2", "Página 002b", "Página 10"], pages.Order(NaturalSort.Instance));
    }

    [Fact]
    public void FileKey_SameContentSameKey_DifferentContentDifferentKey()
    {
        string a = TestBooks.Text(_folder, "a.txt", new string('x', 200_000));
        string b = TestBooks.Text(_folder, "otro nombre.txt", new string('x', 200_000));
        string c = TestBooks.Text(_folder, "c.txt", new string('x', 199_999) + "y");

        Assert.Equal(FileKey.Compute(a), FileKey.Compute(b));
        Assert.NotEqual(FileKey.Compute(a), FileKey.Compute(c));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Epub_TitleAuthorSeriesAndCover(bool epub3Cover)
    {
        string path = TestBooks.Epub(_folder, "x.epub", epub3Cover: epub3Cover);

        BookMetadata metadata = BookMetadata.Read(path, BookFormat.Epub);

        Assert.Equal("El nombre del viento", metadata.Title);
        Assert.Equal("Patrick Rothfuss", metadata.Author);
        Assert.Equal("Crónica del asesino de reyes", metadata.Series);
        Assert.Equal(TestBooks.Png, metadata.Cover);
    }

    [Fact]
    public void Cbz_UsesComicInfo_AndFirstPageInNaturalOrder()
    {
        string path = TestBooks.Cbz(_folder, "saga_03.cbz");

        BookMetadata metadata = BookMetadata.Read(path, BookFormat.Cbz);

        Assert.Equal("Saga #3", metadata.Title);
        Assert.Equal("Brian K. Vaughan", metadata.Author);
        Assert.Equal(TestBooks.Png, metadata.Cover);
    }

    [Fact]
    public void DamagedFile_FallsBackToFileName()
    {
        string path = TestBooks.Text(_folder, "Mi_libro_roto.epub", "esto no es un zip");

        BookMetadata metadata = BookMetadata.Read(path, BookFormat.Epub);

        Assert.Equal("Mi libro roto", metadata.Title);
        Assert.Null(metadata.Cover);
    }

    [Fact]
    public void Reconciler_DetectsAddedMovedChangedAndRemoved()
    {
        Book stays = MakeBook(1, "a.epub", "K1");
        Book renamed = MakeBook(2, "viejo.epub", "K2");
        Book replaced = MakeBook(3, "c.epub", "K3");
        Book deleted = MakeBook(4, "d.epub", "K4");
        ScannedFile[] scanned =
        [
            Scan(1, "A.EPUB", "K1"),          // mismo archivo (Windows no distingue mayúsculas)
            Scan(2, "otra carpeta\\nuevo.epub", "K2"), // movido a otra carpeta enlazada
            Scan(1, "c.epub", "K3-nuevo"),    // reemplazado en el mismo sitio
            Scan(1, "e.epub", "K5"),          // nuevo
        ];

        LibraryChanges changes = LibraryReconciler.Compare([stays, renamed, replaced, deleted], scanned);

        Assert.Equal("e.epub", Assert.Single(changes.Added).RelativePath);
        var moved = Assert.Single(changes.Moved);
        Assert.Equal(2, moved.Book.Id);
        Assert.Equal(2, moved.File.SourceId);
        Assert.Equal(3, Assert.Single(changes.Changed).Book.Id);
        Assert.Equal(4, Assert.Single(changes.Removed).Id);
    }

    [Fact]
    public void SourceLocator_FindsDiskWithNewLetter()
    {
        var source = new LibrarySource(1, @"E:\Libros\Novelas", SourceKind.Linked, "MisLibros");
        DriveSnapshot[] drives = [new(@"C:\", "Windows"), new(@"F:\", "MisLibros")];

        string? found = SourceLocator.Locate(source, drives, path => path == @"F:\Libros\Novelas");

        Assert.Equal(@"F:\Libros\Novelas", found);
    }

    [Fact]
    public void SourceLocator_DisconnectedDisk_GivesNull()
    {
        var source = new LibrarySource(1, @"E:\Libros", SourceKind.Linked, "MisLibros");
        DriveSnapshot[] drives = [new(@"C:\", "Windows"), new(@"E:\", "USB de otra persona")];

        Assert.Null(SourceLocator.Locate(source, drives, _ => true));
    }

    private static Book MakeBook(long id, string path, string key) =>
        new(id, 1, path, key, BookFormat.Epub, 10, DateTime.UnixEpoch, path, null, null, false, DateTime.Now, null, 0);

    private static ScannedFile Scan(long source, string path, string key) => new(source, path, key, BookFormat.Epub, 10, DateTime.UnixEpoch);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
