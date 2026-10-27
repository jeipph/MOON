using Microsoft.Data.Sqlite;
using Moon.Core.Annotations;
using Moon.Core.Library;
using Moon.Core.Tests.Library;

namespace Moon.Core.Tests.Annotations;

public sealed class AnnotationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MOONTests", Guid.NewGuid().ToString("N"));

    private (LibraryService Library, Book Book) NewLibraryWithBook()
    {
        string external = Path.Combine(_root, "Libros");
        TestBooks.Epub(external, "uno.epub");
        var library = new LibraryService(new SqliteLibraryStore(Path.Combine(_root, "moon.db")),
            Path.Combine(_root, "Biblioteca"), Path.Combine(_root, "covers"), () => []);
        library.Link(external);
        return (library, Assert.Single(library.Books));
    }

    private static Annotation Highlight(long bookId, double progress, string text, string note = "") =>
        new(0, bookId, AnnotationKind.Highlight, $"epubcfi(/6/{(int)(progress * 100)})", text, note,
            HighlightColor.Yellow, "Capítulo 1", progress, new DateTime(2026, 10, 27, 12, 0, 0));

    [Fact]
    public void Annotations_AreSaved_Edited_AndDeleted()
    {
        (LibraryService library, Book book) = NewLibraryWithBook();

        Annotation saved = library.AddAnnotation(Highlight(book.Id, 0.2, "Era una noche tranquila"));
        library.AddAnnotation(new Annotation(0, book.Id, AnnotationKind.Region, "page:3", "", "¿Quién es?",
            HighlightColor.Pink, "Página 4", 0.1, DateTime.Now) { Rect = new NormalizedRect(0.1, 0.2, 0.3, 0.4) });
        library.UpdateAnnotation(saved.Id, "Me gusta esta frase", HighlightColor.Green);

        var list = library.GetAnnotations(book.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal(new NormalizedRect(0.1, 0.2, 0.3, 0.4), list[0].Rect); // por avance: la zona va antes
        Annotation edited = list[1];
        Assert.Equal("Me gusta esta frase", edited.Note);
        Assert.Equal(HighlightColor.Green, edited.Color);

        library.DeleteAnnotation(saved.Id);
        Assert.Single(library.GetAnnotations(book.Id));
    }

    [Fact]
    public void RemovingTheBook_RemovesItsAnnotations()
    {
        (LibraryService library, Book book) = NewLibraryWithBook();
        library.AddAnnotation(Highlight(book.Id, 0.2, "texto"));

        library.Remove(book);

        Assert.Empty(library.GetAnnotations(book.Id));
    }

    [Fact]
    public void Markdown_GroupsByChapter_InBookOrder()
    {
        var book = new Book(1, 1, "a.epub", "K", BookFormat.Epub, 1, DateTime.UnixEpoch, "El viento", "Patrick", null,
            false, DateTime.Now, null, 0);
        Annotation late = Highlight(1, 0.8, "Final") with { Chapter = "Capítulo 9" };
        Annotation early = Highlight(1, 0.1, "Línea 1\nLínea 2", "Mi nota");

        string md = AnnotationExporter.ToMarkdown(book, [late, early]);

        Assert.Contains("titulo: \"El viento\"", md);
        Assert.True(md.IndexOf("## Capítulo 1") < md.IndexOf("## Capítulo 9"));
        Assert.Contains("> Línea 1\n> Línea 2".Replace("\n", Environment.NewLine), md);
        Assert.Contains("Mi nota", md);
    }

    [Fact]
    public void Csv_QuotesTextWithSemicolonsAndQuotes()
    {
        var book = new Book(1, 1, "a.epub", "K", BookFormat.Epub, 1, DateTime.UnixEpoch, "Libro", null, null,
            false, DateTime.Now, null, 0);

        string csv = AnnotationExporter.ToCsv(book, [Highlight(1, 0.5, "dijo \"hola\"; y se fue")]);

        Assert.StartsWith("﻿Libro;Tipo;", csv);
        Assert.Contains("\"dijo \"\"hola\"\"; y se fue\"", csv);
        Assert.Contains(";Subrayado;", csv);
    }

    [Theory]
    [InlineData(0.1, 0.1, 0.105, 0.5)] // demasiado estrecho: fue un clic, no un rectángulo
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    public void TinyRectangles_AreIgnored(double x1, double y1, double x2, double y2) =>
        Assert.Null(NormalizedRect.From(x1, y1, x2, y2));

    [Fact]
    public void Rectangle_IsNormalized_AndClampedToThePage()
    {
        NormalizedRect? rect = NormalizedRect.From(0.8, 0.9, 1.4, 0.2); // arrastrado al revés y fuera de la página
        Assert.Equal(new NormalizedRect(0.8, 0.2, 0.2, 0.7), rect!.Value with { Width = Math.Round(rect.Value.Width, 6), Height = Math.Round(rect.Value.Height, 6) });
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
