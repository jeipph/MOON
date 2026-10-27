using Moon.Core.Voice;

namespace Moon.Core.Tests.Voice;

public sealed class SpeechTextTests
{
    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("es", "es")]
    [InlineData("", "es")]
    [InlineData(null, "es")]
    [InlineData("la", "es")] // latín: no lo sabe leer
    public void Language_TwoLetterCode_OrSpanish(string? book, string expected) =>
        Assert.Equal(expected, SpeechText.Language(book));

    [Fact]
    public void ShortText_IsOneChunk() =>
        Assert.Equal(["Hola, ¿qué tal?"], SpeechText.Chunks("  Hola, ¿qué tal?  "));

    [Fact]
    public void LongText_IsCutAtPunctuation_UnderTheLimit()
    {
        string text = string.Join(" ", Enumerable.Repeat("Era una noche muy tranquila, y la luna iluminaba el camino.", 12));

        var chunks = SpeechText.Chunks(text, 120);

        Assert.All(chunks, c => Assert.True(c.Length <= 120, c));
        Assert.All(chunks, c => Assert.EndsWith(".", c));
        Assert.Equal(text.Replace(" ", ""), string.Concat(chunks).Replace(" ", ""));
    }

    [Fact]
    public void TextWithoutPunctuation_IsCutBetweenWords()
    {
        string text = string.Join(" ", Enumerable.Repeat("palabra", 60));
        Assert.All(SpeechText.Chunks(text, 50), c => Assert.DoesNotContain("palab ", c + " "));
    }
}
