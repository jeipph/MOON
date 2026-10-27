namespace Moon.Core.Annotations;

public enum AnnotationKind
{
    /// <summary>Texto subrayado (con nota opcional).</summary>
    Highlight,

    /// <summary>Marcador: "aquí me quedé / esto me interesa".</summary>
    Bookmark,

    /// <summary>Zona de una página de cómic (rectángulo) con nota.</summary>
    Region,
}

public enum HighlightColor
{
    Yellow,
    Green,
    Blue,
    Pink,
    Purple,
}

/// <summary>Rectángulo en proporciones de la página (0 a 1): sirve a cualquier tamaño de pantalla.</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    /// <summary>Recorta a la página y descarta los demasiado pequeños (un clic sin arrastrar).</summary>
    public static NormalizedRect? From(double x1, double y1, double x2, double y2, double minSize = 0.01)
    {
        double left = Math.Clamp(Math.Min(x1, x2), 0, 1), right = Math.Clamp(Math.Max(x1, x2), 0, 1);
        double top = Math.Clamp(Math.Min(y1, y2), 0, 1), bottom = Math.Clamp(Math.Max(y1, y2), 0, 1);
        if (right - left < minSize || bottom - top < minSize)
            return null;
        return new NormalizedRect(left, top, right - left, bottom - top);
    }
}

/// <summary>
/// Un subrayado, marcador o zona marcada. Nunca se escribe en el archivo del libro:
/// vive en la base de datos de MOON (tabla <c>annotations</c>).
/// </summary>
/// <param name="Location">Dónde está: CFI en libros, "page:N" en cómics.</param>
/// <param name="Text">El texto subrayado (en cómics, vacío).</param>
/// <param name="Chapter">Capítulo o página, para mostrar y exportar.</param>
public sealed record Annotation(
    long Id,
    long BookId,
    AnnotationKind Kind,
    string Location,
    string Text,
    string Note,
    HighlightColor Color,
    string Chapter,
    double Progress,
    DateTime CreatedAt)
{
    /// <summary>Solo para zonas de cómic.</summary>
    public NormalizedRect? Rect { get; init; }
}
