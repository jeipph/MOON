using Microsoft.Data.Sqlite;
using Moon.Core.Dictionary;
using Moon.Core.Library;

namespace Moon.Core.Tests.Dictionary;

public sealed class DictionaryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MOONTests", Guid.NewGuid().ToString("N"));

    private static readonly BilingualDictionary Sample = BilingualDictionary.FromLines(
    [
        "# comentario",
        "go\tvblex\tir",
        "go\tn\tturno (m)",
        "child\tn\tniño (m)",
        "run\tvblex\tcorrer",
        "stop\tvblex\tparar",
        "city\tn\tciudad (f)",
        "happy\tadj\tfeliz",
        "quick\tadj\trápido",
        "give up\tvblex\trendirse",
        "give\tvblex\tdar",
        "make\tvblex\thacer",
        "leaf\tn\thoja (f)",
    ]);

    [Theory]
    [InlineData("went", "go")]
    [InlineData("Children", "child")]
    [InlineData("running", "run")]
    [InlineData("stopped", "stop")]
    [InlineData("cities", "city")]
    [InlineData("happier", "happy")]
    [InlineData("quickly", "quick")]
    [InlineData("making", "make")]
    [InlineData("leaves", "leaf")]
    [InlineData("“Go!”", "go")]
    public void Lookup_FindsTheBaseForm(string word, string lemma) =>
        Assert.Equal(lemma, Sample.Lookup(word)?.Lemma);

    [Fact]
    public void Lookup_GroupsByPartOfSpeech_NounsFirst()
    {
        LookupResult result = Sample.Lookup("go")!;

        Assert.Equal(["nombre", "verbo"], result.Senses.Select(s => s.PartOfSpeech));
        Assert.Equal("turno (m), ir", result.Summary);
    }

    [Fact]
    public void Lookup_TwoWordExpressions()
    {
        Assert.Equal("give up", Sample.Lookup("gave", "up")?.Lemma);
        Assert.Equal("give", Sample.Lookup("gave", "it")?.Lemma); // "give it" no existe: solo la palabra
    }

    [Fact]
    public void UnknownWord_GivesNull() => Assert.Null(Sample.Lookup("xyzzy"));

    [Fact]
    public void RealDictionaryFile_Loads()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Moon.App", "Data", "en-es.tsv");
        BilingualDictionary real = BilingualDictionary.Load(path);

        Assert.True(real.WordCount > 25_000);
        Assert.Contains("gato", real.Lookup("cats")!.Summary);
    }

    [Fact]
    public void SentenceAround_FindsTheSentence()
    {
        const string text = "Alice was tired. She saw a White Rabbit with pink eyes! It ran close by her.";
        Assert.Equal("She saw a White Rabbit with pink eyes!", VocabularyExporter.SentenceAround(text, text.IndexOf("Rabbit")));
    }

    [Fact]
    public void Anki_OneCardPerWord_WithoutBreakingColumns()
    {
        var entry = new VocabularyEntry(1, "went", "go", "ir", "She went\tto <the> park.", 1, "Alice", null, DateTime.Now);

        string anki = VocabularyExporter.ToAnki([entry]);

        Assert.Contains("#separator:tab", anki);
        Assert.Contains("went (go)\tir<br><i>She went to &lt;the&gt; park.</i><br><small>Alice</small>", anki);
    }

    [Fact]
    public void Vocabulary_IsSaved_WithoutDuplicates_AndSurvivesRemovingTheBook()
    {
        string external = Path.Combine(_root, "Libros");
        Library.TestBooks.Epub(external, "uno.epub");
        var library = new LibraryService(new SqliteLibraryStore(Path.Combine(_root, "moon.db")),
            Path.Combine(_root, "Biblioteca"), Path.Combine(_root, "covers"), () => []);
        library.Link(external);
        Book book = Assert.Single(library.Books);

        library.AddVocabulary(new VocabularyEntry(0, "went", "go", "ir", "She went home.", book.Id, book.Title, null, DateTime.Now));
        library.AddVocabulary(new VocabularyEntry(0, "goes", "go", "ir", "He goes.", book.Id, book.Title, null, DateTime.Now));
        library.Remove(book);

        VocabularyEntry saved = Assert.Single(library.Vocabulary);
        Assert.Equal("went", saved.Word);
        Assert.Null(saved.BookId);
        Assert.Equal("El nombre del viento", saved.BookTitle);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
