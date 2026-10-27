using System.IO;
using Moon.Core.Dictionary;

namespace Moon.App.Services;

/// <summary>
/// El diccionario inglés → español (Data/en-es.tsv junto a MOON.exe). Se carga la primera vez
/// que se busca una palabra, en segundo plano, para no retrasar la apertura de MOON.
/// </summary>
public sealed class DictionaryService
{
    private readonly Lazy<Task<BilingualDictionary?>> _dictionary = new(() => Task.Run(Load));

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Data", "en-es.tsv");

    /// <summary>Null si la palabra no está (o falta el archivo del diccionario).</summary>
    public async Task<LookupResult?> LookupAsync(string word, string? nextWord = null)
    {
        BilingualDictionary? dictionary = await _dictionary.Value;
        return dictionary?.Lookup(word, nextWord);
    }

    /// <summary>Empieza a cargarlo ya (al abrir un libro), para que la primera búsqueda sea instantánea.</summary>
    public void Preload() => _ = _dictionary.Value;

    private static BilingualDictionary? Load()
    {
        try
        {
            return BilingualDictionary.Load(FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
