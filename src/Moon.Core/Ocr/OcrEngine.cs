using Moon.Core.Annotations;

namespace Moon.Core.Ocr;

/// <summary>Un idioma de OCR instalado ("en-US", "English (United States)").</summary>
public sealed record OcrLanguage(string Tag, string Name)
{
    public bool IsEnglish => Tag.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}

/// <summary>Lee el texto de un trozo de imagen (el recuadro de un bocadillo) sin internet.</summary>
public interface IOcrEngine
{
    /// <summary>Idiomas que puede leer en este PC.</summary>
    IReadOnlyList<OcrLanguage> Languages { get; }

    /// <summary>
    /// Recorta <paramref name="rect"/> de la imagen (JPG, PNG…) a resolución completa, lo agranda
    /// si es pequeño (<see cref="OcrText.Scale"/>) y lo lee.
    /// </summary>
    /// <returns>Las líneas leídas, de arriba abajo (vacía si no hay texto).</returns>
    /// <exception cref="InvalidOperationException">El idioma no está instalado.</exception>
    /// <exception cref="InvalidDataException">La imagen está dañada.</exception>
    Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, NormalizedRect rect, string languageTag);
}
