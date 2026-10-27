namespace Moon.Core.Voice;

/// <summary>Preparar texto para leerlo en voz alta.</summary>
public static class SpeechText
{
    /// <summary>Idiomas que Chatterbox sabe leer (código de dos letras).</summary>
    private static readonly HashSet<string> Supported =
        ["ar", "da", "de", "el", "en", "es", "fi", "fr", "he", "hi", "it", "ja", "ko", "ms", "nl", "no", "pl", "pt", "ru", "sv", "sw", "tr", "zh"];

    /// <summary>"en-US" → "en"; si el libro no lo dice o es un idioma que no sabe, español.</summary>
    public static string Language(string? bookLanguage)
    {
        string code = (bookLanguage ?? "").Trim().ToLowerInvariant();
        if (code.Length >= 2)
            code = code[..2];
        return Supported.Contains(code) ? code : "es";
    }

    /// <summary>
    /// Parte un texto largo en trozos de como mucho <paramref name="maxLength"/> caracteres,
    /// cortando en signos de puntuación (y si no hay, entre palabras). Las voces generadas
    /// con IA se equivocan más con frases muy largas.
    /// </summary>
    public static IReadOnlyList<string> Chunks(string text, int maxLength = 260)
    {
        text = text.Trim();
        var chunks = new List<string>();
        while (text.Length > maxLength)
        {
            int cut = LastBreak(text, maxLength, ".!?;:") is var strong and > 0 ? strong
                : LastBreak(text, maxLength, ",—–)") is var weak and > 0 ? weak
                : text.LastIndexOf(' ', maxLength) is var space and > 0 ? space
                : maxLength;
            chunks.Add(text[..cut].Trim());
            text = text[cut..].Trim();
        }
        if (text.Length > 0)
            chunks.Add(text);
        return chunks;
    }

    // Último signo de la lista antes del límite (incluido el signo en el trozo); 0 si no hay o está muy al principio.
    private static int LastBreak(string text, int maxLength, string marks)
    {
        int index = text.LastIndexOfAny(marks.ToCharArray(), maxLength - 1);
        return index > maxLength / 3 ? index + 1 : 0;
    }
}
