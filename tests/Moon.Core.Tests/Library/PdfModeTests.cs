using Moon.Core.Comics;
using Moon.Core.Library;

namespace Moon.Core.Tests.Library;

public sealed class PdfModeTests
{
    private static Book NewBook(BookFormat format, string? comicView = null) =>
        new(1, 1, "x", "k", format, 10, DateTime.UtcNow, "Libro", null, null, false, DateTime.UtcNow, null, 0) { ComicView = comicView };

    [Fact]
    public void Pdf_OpensAsImages_ByDefault()
    {
        Book pdf = NewBook(BookFormat.Pdf);

        Assert.True(pdf.OpensAsComic);
        Assert.False(pdf.OpensAsBook);
        Assert.False(pdf.IsPdfDocument);
    }

    [Fact]
    public void Pdf_ChosenAsDocument_OpensInTheBookReader()
    {
        string view = (ComicView.Default(isManga: false) with { PdfAsDocument = true }).ToJson();
        Book pdf = NewBook(BookFormat.Pdf, view);

        Assert.True(pdf.IsPdfDocument);
        Assert.True(pdf.OpensAsBook);
        Assert.False(pdf.OpensAsComic);
        Assert.True(ComicView.FromJson(view)!.PdfAsDocument); // se guarda y se vuelve a leer
    }

    [Fact]
    public void OnlyPdfs_CanBeDocuments()
    {
        string view = (ComicView.Default(isManga: false) with { PdfAsDocument = true }).ToJson();

        Assert.True(NewBook(BookFormat.Cbz, view).OpensAsComic);
        Assert.False(NewBook(BookFormat.Cbz, view).IsPdfDocument);
    }

    [Fact]
    public void OldSavedViews_WithoutTheField_StayAsImages() =>
        Assert.False(ComicView.FromJson("""{"Direction":"RightToLeft","Adjustments":{"Brightness":0,"Contrast":0,"Gamma":100,"Whiten":false}}""")?.PdfAsDocument);
}
