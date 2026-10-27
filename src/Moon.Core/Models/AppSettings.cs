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
