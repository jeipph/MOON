using System.Text;

namespace Moon.Core.Dictionary;

/// <summary>Una palabra guardada para estudiar, con la frase donde apareció.</summary>
public sealed record VocabularyEntry(
    long Id,
    string Word,
    string Lemma,
    string Translation,
    string Context,
    long? BookId,
    string BookTitle,
    string? Location,
    DateTime CreatedAt);

public static class VocabularyExporter
{
    /// <summary>
    /// Texto para importar en Anki (Archivo → Importar): una tarjeta por palabra,
    /// separada por tabuladores. Delante: la palabra; detrás: traducción, frase y libro.
    /// </summary>
    public static string ToAnki(IEnumerable<VocabularyEntry> entries)
    {
        var text = new StringBuilder();
        text.AppendLine("#separator:tab");
        text.AppendLine("#html:true");
        text.AppendLine("#columns:Palabra\tTraducción y contexto");
        foreach (VocabularyEntry e in entries)
        {
            string front = Clean(e.Lemma == e.Word ? e.Word : $"{e.Word} ({e.Lemma})");
            string back = Clean(e.Translation);
            if (!string.IsNullOrWhiteSpace(e.Context))
                back += $"<br><i>{Clean(e.Context)}</i>";
            if (!string.IsNullOrWhiteSpace(e.BookTitle))
                back += $"<br><small>{Clean(e.BookTitle)}</small>";
            text.Append(front).Append('\t').AppendLine(back);
        }
        return text.ToString();
    }

    // Sin tabuladores ni saltos de línea (romperían las columnas) y sin HTML del texto original.
    private static string Clean(string value) =>
        value.Replace('\t', ' ').Replace("\r", "").Replace('\n', ' ')
             .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Trim();

    /// <summary>La frase que contiene la palabra (para recordar en qué contexto apareció).</summary>
    public static string SentenceAround(string text, int index, int maxLength = 240)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        index = Math.Clamp(index, 0, text.Length - 1);
        int start = text.LastIndexOfAny(['.', '!', '?', '\n'], index);
        int end = text.IndexOfAny(['.', '!', '?', '\n'], index);
        start = start < 0 ? 0 : start + 1;
        end = end < 0 ? text.Length : end + 1;
        string sentence = text[start..end].Trim();
        return sentence.Length <= maxLength ? sentence : sentence[..maxLength].TrimEnd() + "…";
    }
}
