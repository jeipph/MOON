namespace Moon.Core.Research;

/// <summary>
/// Una "cosa por investigar": el recorte de una página (guardado como PNG aparte) con una nota.
/// Se conserva aunque el libro se quite de la biblioteca.
/// </summary>
/// <param name="Location">Dónde estaba ("page:12" en cómics).</param>
/// <param name="Where">Para mostrar: "Página 12".</param>
public sealed record ResearchItem(
    long Id,
    long? BookId,
    string BookTitle,
    string? Location,
    string Where,
    string ImageFile,
    string Note,
    DateTime CreatedAt);

/// <summary>Una web de búsqueda por imagen. MOON solo la abre: la persona pega el recorte allí.</summary>
public sealed record ImageSearchSite(string Name, string Url, string Hint);

public static class ImageSearchSites
{
    /// <summary>
    /// Las webs que aceptan pegar una imagen con Ctrl+V (probado en el spike S4: no hace falta subir
    /// un archivo, y así MOON nunca envía nada por su cuenta).
    /// </summary>
    public static IReadOnlyList<ImageSearchSite> All { get; } =
    [
        new("Google Lens", "https://lens.google.com/", "Objetos, lugares, personajes y texto"),
        new("Bing", "https://www.bing.com/visualsearch", "Parecido a Google Lens"),
        new("SauceNAO", "https://saucenao.com/", "El origen de una imagen de manga o anime"),
        new("TinEye", "https://tineye.com/", "Dónde más aparece esta imagen exacta"),
    ];

    /// <summary>Nombre del archivo del recorte: fecha y hora, sin caracteres raros.</summary>
    public static string FileNameFor(DateTime createdAt) => $"recorte-{createdAt:yyyyMMdd-HHmmss-fff}.png";
}
