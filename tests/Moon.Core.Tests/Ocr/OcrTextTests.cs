using Moon.Core.Annotations;
using Moon.Core.Ocr;

namespace Moon.Core.Tests.Ocr;

public sealed class OcrTextTests
{
    [Fact]
    public void Lines_AreJoinedIntoOneParagraph() =>
        Assert.Equal("WE CAN'T STAY HERE. THE DEAD ARE COMING", OcrText.JoinLines(["WE CAN'T STAY HERE.", "  THE DEAD  ARE", "", "COMING "]));

    [Fact]
    public void HyphenatedWord_IsJoined() =>
        Assert.Equal("NO AMMUNITION LEFT", OcrText.JoinLines(["NO AMMU-", "NITION LEFT"]));

    [Fact]
    public void Dashes_AreKept() =>
        Assert.Equal("WELL-- I GUESS", OcrText.JoinLines(["WELL--", "I GUESS"]));

    [Theory]
    [InlineData("WE CANIT STAY", "WE CAN'T STAY")]
    [InlineData("I DONlT KNOW", "I DON'T KNOW")]
    [InlineData("IIM SORRY", "I'M SORRY")]
    [InlineData("THEY|RE HERE", "THEY'RE HERE")]
    [InlineData("You dont say", "You don't say")]
    [InlineData("Its fine, he held it well", "Its fine, he held it well")] // palabras reales: no se tocan
    public void Contractions_AreRepaired(string ocr, string expected) =>
        Assert.Equal(expected, OcrText.FixContractions(ocr));

    [Fact]
    public void ComicCaps_BecomeSentenceCase() =>
        Assert.Equal("We can't stay here. I think it's over! Where are you?",
            OcrText.ToSentenceCase("WE CAN'T STAY HERE. I THINK IT'S OVER! WHERE ARE YOU?"));

    [Fact]
    public void NormalText_IsLeftAlone() =>
        Assert.Equal("Hello, NASA called Rick.", OcrText.ToSentenceCase("Hello, NASA called Rick."));

    [Theory]
    [InlineData("iOOF!", "¡OOF!")]
    [InlineData("|NO TE MUEVAS!", "¡NO TE MUEVAS!")]
    [InlineData("iPhone ready!", "iPhone ready!")] // no es una palabra en mayúsculas
    [InlineData("iOOF?", "iOOF?")]                 // sin "!" no se toca
    public void InvertedExclamation_IsRepaired(string ocr, string expected) =>
        Assert.Equal(expected, OcrText.FixInvertedMarks(ocr));

    [Fact]
    public void Clean_DoesEverything() =>
        Assert.Equal("I'm out of ammunition.", OcrText.Clean(["IIM OUT OF AMMU-", "NITION."]));

    [Fact]
    public void Rect_HasMargin_AndStaysInside()
    {
        PixelRect r = OcrText.ToPixels(new NormalizedRect(0.5, 0.5, 0.5, 0.5), 1000, 2000);
        Assert.Equal(480, r.X);
        Assert.Equal(960, r.Y);
        Assert.Equal(1000, r.X + r.Width);
        Assert.Equal(2000, r.Y + r.Height);
    }

    [Theory]
    [InlineData(200, 100, 10000, 2.0)]   // muy pequeño: como mucho ×2
    [InlineData(800, 300, 10000, 1.25)]
    [InlineData(3000, 500, 10000, 1.0)]  // ya es grande: no se toca
    [InlineData(400, 200, 600, 1.5)]     // sin pasarse del máximo del motor
    [InlineData(20000, 100, 10000, 0.5)] // más grande que el máximo: se reduce
    public void Scale_GrowsSmallCrops(int width, int height, int max, double expected) =>
        Assert.Equal(expected, OcrText.Scale(width, height, max), 3);

    [Fact]
    public void WordAt_FindsWordAndNext()
    {
        Assert.Equal(("give", "up"), OcrText.WordAt("Don't give up now", 7));
        Assert.Equal(("Don't", "give"), OcrText.WordAt("Don't give up now", 2));
        Assert.Null(OcrText.WordAt("Hi  there", 3));
    }
}
