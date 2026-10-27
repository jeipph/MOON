namespace Moon.Core.Models;

public enum AppTheme
{
    /// <summary>Sigue el modo claro/oscuro de Windows.</summary>
    System,
    Dark,
    Light,
}

/// <summary>Opciones de MOON. Se guardan en <c>%APPDATA%\MOON\settings.json</c>.</summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;

    public ReaderSettings Reader { get; init; } = new();

    public ComicSettings Comic { get; init; } = new();

    public VoiceSettings Voice { get; init; } = new();
}

/// <summary>Lectura en voz alta.</summary>
public sealed record VoiceSettings
{
    /// <summary>Nombre de la voz (archivo de la carpeta de voces) o "" = la primera que haya.</summary>
    public string Voice { get; init; } = "";

    /// <summary>Velocidad: 1 = normal.</summary>
    public double Speed { get; init; } = 1.0;
}

public enum ComicFit
{
    /// <summary>Se ve la página entera.</summary>
    Page,

    /// <summary>La página ocupa todo el ancho (se desplaza hacia abajo).</summary>
    Width,
}

/// <summary>Cómo se ven los cómics y el manga. Iguales para todos; la dirección y los ajustes de imagen van por cómic.</summary>
public sealed record ComicSettings
{
    public Moon.Core.Comics.PageLayout Layout { get; init; } = Moon.Core.Comics.PageLayout.Single;

    public ComicFit Fit { get; init; } = ComicFit.Page;

    /// <summary>En doble página, la portada va sola (como un tomo de verdad).</summary>
    public bool CoverAlone { get; init; } = true;
}

/// <summary>Color de la página del libro (independiente del tema de la ventana).</summary>
public enum PageTheme
{
    /// <summary>Claro u oscuro según el tema de MOON.</summary>
    Auto,
    Light,
    Sepia,
    Dark,
}

public enum PageFont
{
    /// <summary>La letra que trae el libro.</summary>
    Original,
    Serif,
    Sans,
}

public enum PageMargins
{
    Narrow,
    Normal,
    Wide,
}

/// <summary>Cómo se ven los libros. Iguales para todos los libros.</summary>
public sealed record ReaderSettings
{
    public const int MinFontSize = 70;
    public const int MaxFontSize = 200;
    public const int FontSizeStep = 10;

    /// <summary>Tamaño de letra en % (100 = el del libro).</summary>
    public int FontSize { get; init; } = 100;

    public PageFont Font { get; init; } = PageFont.Original;

    public PageMargins Margins { get; init; } = PageMargins.Normal;

    public PageTheme Theme { get; init; } = PageTheme.Auto;

    /// <summary>True = pasar páginas; false = desplazamiento continuo.</summary>
    public bool Paginated { get; init; } = true;

    public ReaderSettings WithFontSize(int size) =>
        this with { FontSize = Math.Clamp(size, MinFontSize, MaxFontSize) };
}
