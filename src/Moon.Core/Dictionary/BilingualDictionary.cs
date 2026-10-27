namespace Moon.Core.Dictionary;

/// <summary>Traducciones de una palabra para una categoría ("verbo": romper, quebrar…).</summary>
public sealed record Sense(string PartOfSpeech, IReadOnlyList<string> Translations);

/// <summary>Resultado de buscar una palabra: la forma encontrada y sus traducciones por categoría.</summary>
/// <param name="Word">Lo que se buscó ("went").</param>
/// <param name="Lemma">La forma del diccionario ("go").</param>
public sealed record LookupResult(string Word, string Lemma, IReadOnlyList<Sense> Senses)
{
    /// <summary>Resumen en una línea, para la lista de vocabulario: "ir, marchar".</summary>
    public string Summary => string.Join(", ", Senses.SelectMany(s => s.Translations).Distinct().Take(4));
}

/// <summary>
/// Diccionario inglés → español sin internet (archivo TSV: inglés, categoría, español).
/// Se carga una vez en memoria (~30 000 palabras, unos pocos MB).
/// </summary>
public sealed class BilingualDictionary
{
    private readonly Dictionary<string, List<(string Pos, string Translation)>> _entries = new(StringComparer.Ordinal);

    private BilingualDictionary() { }

    public int WordCount => _entries.Count;

    public static BilingualDictionary Load(string path) => FromLines(File.ReadLines(path));

    public static BilingualDictionary FromLines(IEnumerable<string> lines)
    {
        var dictionary = new BilingualDictionary();
        foreach (string line in lines)
        {
            if (line.Length == 0 || line[0] == '#')
                continue;
            string[] parts = line.Split('\t');
            if (parts.Length < 3)
                continue;
            if (!dictionary._entries.TryGetValue(parts[0], out var list))
                dictionary._entries[parts[0]] = list = [];
            list.Add((parts[1], parts[2]));
        }
        return dictionary;
    }

    /// <summary>
    /// Busca la palabra o su forma base. También prueba expresiones de dos palabras
    /// si se pasa la siguiente ("give up"). Null si no está.
    /// </summary>
    public LookupResult? Lookup(string word, string? nextWord = null)
    {
        if (nextWord is not null)
        {
            foreach (string first in EnglishLemmatizer.Candidates(word))
            {
                string phrase = $"{first} {EnglishLemmatizer.Clean(nextWord)}";
                if (_entries.TryGetValue(phrase, out var phraseEntries))
                    return Build(word, phrase, phraseEntries);
            }
        }
        foreach (string candidate in EnglishLemmatizer.Candidates(word))
        {
            if (_entries.TryGetValue(candidate, out var entries))
                return Build(word, candidate, entries);
        }
        return null;
    }

    private static LookupResult Build(string word, string lemma, List<(string Pos, string Translation)> entries)
    {
        var senses = entries
            .GroupBy(e => e.Pos)
            .OrderBy(g => PosOrder(g.Key))
            .Select(g => new Sense(PosName(g.Key), [.. g.Select(e => e.Translation).Distinct()]))
            .ToList();
        return new LookupResult(word, lemma, senses);
    }

    /// <summary>Nombre en español de las categorías de Apertium.</summary>
    public static string PosName(string tag) => tag switch
    {
        "n" => "nombre",
        "np" => "nombre propio",
        "vblex" or "vbser" or "vbhaver" or "vbmod" or "vaux" => "verbo",
        "adj" => "adjetivo",
        "adv" or "cnjadv" => "adverbio",
        "pr" => "preposición",
        "prn" => "pronombre",
        "det" => "determinante",
        "cnjcoo" or "cnjsub" => "conjunción",
        "ij" => "interjección",
        "num" => "número",
        _ => "otro",
    };

    // Primero lo más útil al leer: nombres, verbos, adjetivos, adverbios.
    private static int PosOrder(string tag) => PosName(tag) switch
    {
        "nombre" => 0,
        "verbo" => 1,
        "adjetivo" => 2,
        "adverbio" => 3,
        _ => 4,
    };
}
