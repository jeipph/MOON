using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moon.Core.Comics;

/// <summary>
/// Lo que se recuerda de cada cómic además de la página: dirección de lectura y ajustes de imagen.
/// Se guarda como JSON en la base de datos (columna <c>comic_view</c>).
/// </summary>
public sealed record ComicView(ReadingDirection Direction, ImageAdjustments Adjustments)
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public static ComicView Default(bool isManga) =>
        new(isManga ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight, ImageAdjustments.None);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Null si no hay nada guardado o está dañado (se usará el valor por defecto).</summary>
    public static ComicView? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<ComicView>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Posición en un cómic: "page:12" (página 13).</summary>
    public static string PageLocation(int page) => $"page:{page}";

    public static int? PageFromLocation(string? location) =>
        location is not null && location.StartsWith("page:", StringComparison.Ordinal) && int.TryParse(location.AsSpan(5), out int page)
            ? page
            : null;
}
