namespace Moon.Core.Ocr;

/// <summary>Un idioma de OCR instalado ("en-US", "English (United States)").</summary>
public sealed record OcrLanguage(string Tag, string Name)
{
    public bool IsEnglish => Tag.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}

/// <summary>Una imagen en memoria, 4 bytes por píxel (azul, verde, rojo, alfa), fila tras fila.</summary>
public sealed record BgraImage(byte[] Pixels, int Width, int Height);

/// <summary>Lee el texto de una imagen (el recorte de un bocadillo) sin internet.</summary>
public interface IOcrEngine
{
    /// <summary>Idiomas que puede leer en este PC.</summary>
    IReadOnlyList<OcrLanguage> Languages { get; }

    /// <summary>Lado más largo que admite (las imágenes mayores se reducen antes).</summary>
    int MaxImageSize { get; }

    /// <returns>Las líneas leídas, de arriba abajo (vacía si no hay texto).</returns>
    /// <exception cref="InvalidOperationException">El idioma no está instalado.</exception>
    Task<IReadOnlyList<string>> ReadLinesAsync(BgraImage image, string languageTag);
}
