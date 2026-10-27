using System.IO.Compression;
using Moon.Core.Comics;
using Moon.Core.Tests.Library;

namespace Moon.Core.Tests.Comics;

public sealed class ComicLogicTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MOONTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Single_OnePagePerSpread()
    {
        var spreads = SpreadLayout.Build(3, PageLayout.Single, _ => false);
        Assert.Equal([[0], [1], [2]], spreads);
    }

    [Fact]
    public void Double_CoverAlone_ThenPairs()
    {
        var spreads = SpreadLayout.Build(6, PageLayout.Double, _ => false);
        Assert.Equal([[0], [1, 2], [3, 4], [5]], spreads);
    }

    [Fact]
    public void Double_WidePagesGoAlone()
    {
        // La página 3 es una ilustración panorámica (ya ocupa dos páginas).
        var spreads = SpreadLayout.Build(7, PageLayout.Double, page => page == 3);
        Assert.Equal([[0], [1, 2], [3], [4, 5], [6]], spreads);
    }

    [Fact]
    public void Double_WithoutCoverAlone()
    {
        var spreads = SpreadLayout.Build(4, PageLayout.Double, _ => false, coverAlone: false);
        Assert.Equal([[0, 1], [2, 3]], spreads);
    }

    [Fact]
    public void Manga_ShowsFirstPageOnTheRight()
    {
        Assert.Equal([2, 1], SpreadLayout.OnScreen([1, 2], ReadingDirection.RightToLeft));
        Assert.Equal([1, 2], SpreadLayout.OnScreen([1, 2], ReadingDirection.LeftToRight));
    }

    [Fact]
    public void SpreadOf_FindsThePage()
    {
        var spreads = SpreadLayout.Build(6, PageLayout.Double, _ => false);
        Assert.Equal(2, SpreadLayout.SpreadOf(spreads, 4));
        Assert.Equal(0, SpreadLayout.SpreadOf(spreads, 0));
    }

    [Fact]
    public void Adjustments_NoneLeavesColorsAlone()
    {
        byte[] table = ImageAdjustments.None.BuildTable();
        for (int c = 0; c < 256; c++)
            Assert.Equal(c, table[c]);
    }

    [Fact]
    public void Adjustments_BrightnessAndGamma()
    {
        byte[] brighter = new ImageAdjustments(Brightness: 20).BuildTable();
        Assert.True(brighter[100] > 100);
        Assert.Equal(255, brighter[255]);

        byte[] lighterMids = new ImageAdjustments(Gamma: 150).BuildTable();
        Assert.True(lighterMids[128] > 128);
        Assert.Equal(0, lighterMids[0]);
        Assert.Equal(255, lighterMids[255]);
    }

    [Fact]
    public void Whiten_StretchesYellowPaperToWhite()
    {
        // Papel amarillento: el azul nunca pasa de 200. Con niveles, 200 pasa a ser blanco.
        var histogram = new int[256];
        for (int c = 20; c <= 200; c++)
            histogram[c] = 100;
        (byte low, byte high) = ImageAdjustments.Levels(histogram);
        byte[] table = new ImageAdjustments(Whiten: true).BuildTable(low, high);

        Assert.InRange(high, 195, 200);
        Assert.Equal(255, table[200]);
        Assert.Equal(0, table[low]);
    }

    [Fact]
    public void ComicView_RoundTrip_AndDamagedJson()
    {
        var view = new ComicView(ReadingDirection.RightToLeft, new ImageAdjustments(10, -5, 120, true));
        Assert.Equal(view, ComicView.FromJson(view.ToJson()));
        Assert.Null(ComicView.FromJson("{ roto"));
        Assert.Equal(12, ComicView.PageFromLocation(ComicView.PageLocation(12)));
        Assert.Null(ComicView.PageFromLocation("epubcfi(/6/2)"));
    }

    [Fact]
    public void ZipComic_PagesInNaturalOrder_IgnoringJunk()
    {
        string path = TestBooks.Cbz(_folder, "saga.cbz");
        using IPageSource pages = PageSources.OpenArchive(path);

        Assert.Equal(2, pages.Count); // pag2 y pag10; la de __MACOSX no cuenta
        Assert.Equal("pag2.png", pages.Name(0));
        Assert.Equal(TestBooks.Png, pages.Read(0, 0));
    }

    [Fact]
    public void ArchiveWithZipInsideCbrName_IsStillOpened()
    {
        // Un ".cbr" que en realidad es ZIP (pasa mucho): se mira el contenido, no la extensión.
        string zip = TestBooks.Cbz(_folder, "falso.cbr");
        using IPageSource pages = PageSources.OpenArchive(zip);
        Assert.Equal(2, pages.Count);
    }

    [Fact]
    public void FolderOfImages_Works_AndEmptyArchiveFails()
    {
        string folder = Path.Combine(_folder, "capitulo 1");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "10.png"), TestBooks.Png);
        File.WriteAllBytes(Path.Combine(folder, "9.png"), TestBooks.Png);
        using (IPageSource pages = PageSources.OpenArchive(folder))
            Assert.Equal("9.png", pages.Name(0));

        string empty = Path.Combine(_folder, "vacio.cbz");
        using (ZipArchive zip = ZipFile.Open(empty, ZipArchiveMode.Create))
            zip.CreateEntry("leeme.txt");
        Assert.Throws<InvalidDataException>(() => PageSources.OpenArchive(empty));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
