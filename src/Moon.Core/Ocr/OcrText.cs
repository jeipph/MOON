using System.Text;
using System.Text.RegularExpressions;
using Moon.Core.Annotations;

namespace Moon.Core.Ocr;

/// <summary>Rectángulo en píxeles de la imagen original.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Arregla lo que devuelve el OCR para leerlo y buscarlo en el diccionario: une las líneas de un
/// bocadillo, repara los apóstrofos mal leídos ("CANIT" → "CAN'T") y quita las mayúsculas de cómic.
/// </summary>
public static partial class OcrText
{
    // Los recortes pequeños se agrandan hasta este lado, como mucho ×2: en el spike S1 (bocadillos de
    // The Walking Dead) ×2 leyó mejor que ×1, ×3 y ×4 (al agrandar más, las letras se emborronan).
    private const int TargetLongSide = 1000;
    private const double MaxUpscale = 2;
    // Margen alrededor del recorte, en proporción: si el rectángulo corta una letra, el OCR la pierde.
    private const double Padding = 0.04;

    // Contracciones inglesas: el OCR suele leer el apóstrofo como I, l, 1 o |.
    private static readonly string[] Contractions =
    [
        "can't", "don't", "won't", "isn't", "aren't", "wasn't", "weren't", "didn't", "doesn't", "haven't",
        "hasn't", "hadn't", "couldn't", "wouldn't", "shouldn't", "mustn't", "ain't", "needn't",
        "i'm", "i'll", "i've", "i'd", "you're", "you'll", "you've", "you'd", "he's", "he'll", "he'd",
        "she's", "she'll", "she'd", "it's", "it'll", "we're", "we'll", "we've", "we'd", "they're",
        "they'll", "they've", "they'd", "that's", "there's", "what's", "who's", "where's", "let's",
        "here's", "how's", "y'all", "c'mon", "o'clock",
    ];

    private static readonly Dictionary<string, string> Misread = BuildMisread();

    /// <summary>Las líneas de un bocadillo como un solo párrafo (con las palabras cortadas por guion unidas).</summary>
    public static string JoinLines(IEnumerable<string> lines)
    {
        var text = new StringBuilder();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0)
                continue;
            // "AMMU-" + "NITION" → "AMMUNITION" (pero "WELL--" o "-" sueltos se dejan).
            if (text.Length > 1 && text[^1] == '-' && char.IsLetter(text[^2]) && char.IsLetter(line[0]))
                text.Length--;
            else if (text.Length > 0)
                text.Append(' ');
            text.Append(line);
        }
        return Spaces().Replace(text.ToString(), " ");
    }

    /// <summary>"CANIT" → "CAN'T", "IIM" → "I'M". Conserva las mayúsculas tal cual.</summary>
    public static string FixContractions(string text) =>
        Word().Replace(text, match => Misread.TryGetValue(match.Value.ToLowerInvariant(), out string? fixedWord)
            ? MatchCase(fixedWord, match.Value)
            : match.Value);

    /// <summary>
    /// Si casi todo está en mayúsculas (como en los cómics), lo pasa a minúsculas con mayúscula
    /// al empezar cada frase y en "I". Si no, lo deja como está.
    /// </summary>
    public static string ToSentenceCase(string text)
    {
        int letters = text.Count(char.IsLetter);
        if (letters == 0 || text.Count(char.IsUpper) < letters * 0.8)
            return text;
        var result = new StringBuilder(text.ToLowerInvariant());
        bool startOfSentence = true;
        for (int i = 0; i < result.Length; i++)
        {
            char c = result[i];
            if (char.IsLetter(c))
            {
                bool standaloneI = c == 'i' && (i == 0 || !char.IsLetter(result[i - 1])) && (i + 1 == result.Length || !char.IsLetter(result[i + 1]));
                if (startOfSentence || standaloneI)
                    result[i] = char.ToUpperInvariant(c);
                startOfSentence = false;
            }
            else if (c is '.' or '!' or '?')
                startOfSentence = true;
        }
        return result.ToString();
    }

    /// <summary>"iOOF!" → "¡OOF!": el OCR lee la ¡ de los cómics en español como una i (o | o 1).</summary>
    public static string FixInvertedMarks(string text) => InvertedExclamation().Replace(text, "¡");

    /// <summary>Todo junto: líneas del OCR → texto listo para leer.</summary>
    public static string Clean(IEnumerable<string> lines) =>
        ToSentenceCase(FixContractions(FixInvertedMarks(JoinLines(lines))));

    /// <summary>El rectángulo marcado, en píxeles de la imagen, con un pequeño margen y sin salirse.</summary>
    public static PixelRect ToPixels(NormalizedRect rect, int imageWidth, int imageHeight)
    {
        double padX = rect.Width * Padding, padY = rect.Height * Padding;
        int left = (int)Math.Floor(Math.Clamp(rect.X - padX, 0, 1) * imageWidth);
        int top = (int)Math.Floor(Math.Clamp(rect.Y - padY, 0, 1) * imageHeight);
        int right = (int)Math.Ceiling(Math.Clamp(rect.X + rect.Width + padX, 0, 1) * imageWidth);
        int bottom = (int)Math.Ceiling(Math.Clamp(rect.Y + rect.Height + padY, 0, 1) * imageHeight);
        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>Cuánto agrandar (o reducir) el recorte antes de leerlo.</summary>
    public static double Scale(int width, int height, int maxSize)
    {
        int longSide = Math.Max(width, height);
        if (longSide <= 0)
            return 1;
        double scale = Math.Clamp((double)TargetLongSide / longSide, 1, MaxUpscale);
        return Math.Min(scale, (double)maxSize / longSide);
    }

    /// <summary>La palabra en la posición indicada del texto (para buscarla con un clic), y la que le sigue.</summary>
    public static (string Word, string? Next)? WordAt(string text, int index)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        index = Math.Clamp(index, 0, text.Length);
        int start = index, end = index;
        while (start > 0 && IsWordChar(text[start - 1])) start--;
        while (end < text.Length && IsWordChar(text[end])) end++;
        if (start == end)
            return null;
        string word = text[start..end].Trim('\'');
        Match next = Word().Match(text, end);
        return word.Length == 0 ? null : (word, next.Success ? next.Value : null);
    }

    private static bool IsWordChar(char c) => char.IsLetter(c) || c is '\'' or '’';

    // "DONlT" cuenta como mayúsculas aunque la l (el apóstrofo mal leído) sea minúscula.
    private static string MatchCase(string word, string original) =>
        original.Count(char.IsUpper) >= Math.Max(2, original.Count(char.IsLetter) - 1) ? word.ToUpperInvariant()
        : char.IsUpper(original[0]) ? char.ToUpperInvariant(word[0]) + word[1..]
        : word;

    private static Dictionary<string, string> BuildMisread()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string contraction in Contractions)
        {
            foreach (string wrong in (string[])["i", "l", "1", "|", ""])
            {
                string key = contraction.Replace("'", wrong);
                // "its", "well", "were", "hell", "id", "ill", "shell"… existen: sin apóstrofo no se tocan.
                if (wrong.Length == 0 && contraction is not ("can't" or "don't" or "won't" or "isn't" or "didn't"
                    or "doesn't" or "couldn't" or "wouldn't" or "shouldn't" or "wasn't" or "aren't" or "haven't" or "i'm" or "you're" or "they're"))
                    continue;
                if (key is "held" or "weld") // "he'd" y "we'd" con l: son palabras de verdad
                    continue;
                map.TryAdd(key, contraction);
            }
        }
        return map;
    }

    [GeneratedRegex(@"[\p{L}0-9|']+")]
    private static partial Regex Word();

    // Una i minúscula (o | o 1) pegada a una palabra en mayúsculas que acaba en "!".
    [GeneratedRegex(@"(?<!\p{L})[i|1](?=\p{Lu}{2,}[\p{Lu}\p{P}\s]*!)")]
    private static partial Regex InvertedExclamation();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
