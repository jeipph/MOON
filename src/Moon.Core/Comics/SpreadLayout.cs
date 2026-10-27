namespace Moon.Core.Comics;

public enum PageLayout
{
    /// <summary>Una página cada vez.</summary>
    Single,

    /// <summary>Dos páginas juntas, como un libro abierto.</summary>
    Double,

    /// <summary>Todas las páginas una debajo de otra (webtoon / manhwa).</summary>
    Webtoon,
}

public enum ReadingDirection
{
    /// <summary>Izquierda → derecha (cómic occidental).</summary>
    LeftToRight,

    /// <summary>Derecha → izquierda (manga).</summary>
    RightToLeft,
}

/// <summary>
/// Agrupa las páginas en "pliegos" (lo que se ve de una vez). En doble página, la portada
/// y las ilustraciones panorámicas (más anchas que altas) van solas, como en un tomo de verdad.
/// </summary>
public static class SpreadLayout
{
    /// <param name="isWide">Si la página es panorámica (ya ocupa las dos páginas).</param>
    /// <returns>Cada pliego con sus páginas en orden de lectura (no de pantalla).</returns>
    public static IReadOnlyList<int[]> Build(int pageCount, PageLayout layout, Func<int, bool> isWide, bool coverAlone = true)
    {
        var spreads = new List<int[]>();
        if (layout != PageLayout.Double)
        {
            for (int i = 0; i < pageCount; i++)
                spreads.Add([i]);
            return spreads;
        }

        int page = 0;
        if (coverAlone && pageCount > 0)
            spreads.Add([page++]);
        while (page < pageCount)
        {
            bool canPair = page + 1 < pageCount && !isWide(page) && !isWide(page + 1);
            spreads.Add(canPair ? [page, page + 1] : [page]);
            page += canPair ? 2 : 1;
        }
        return spreads;
    }

    /// <summary>El pliego que contiene esta página.</summary>
    public static int SpreadOf(IReadOnlyList<int[]> spreads, int page)
    {
        for (int i = 0; i < spreads.Count; i++)
        {
            if (spreads[i].Contains(page))
                return i;
        }
        return Math.Clamp(spreads.Count - 1, 0, int.MaxValue);
    }

    /// <summary>Orden en pantalla, de izquierda a derecha: en manga la primera página va a la derecha.</summary>
    public static int[] OnScreen(int[] spread, ReadingDirection direction) =>
        direction == ReadingDirection.RightToLeft ? [.. spread.Reverse()] : spread;
}
