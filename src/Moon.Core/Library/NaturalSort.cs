namespace Moon.Core.Library;

/// <summary>
/// Orden "natural": "Página 2" va antes que "Página 10" (el orden alfabético normal los pone al revés).
/// Sirve para las páginas de los cómics y para los nombres de archivo.
/// </summary>
public sealed class NaturalSort : IComparer<string>
{
    public static readonly NaturalSort Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int startX = i, startY = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                int result = CompareNumbers(x.AsSpan(startX, i - startX), y.AsSpan(startY, j - startY));
                if (result != 0) return result;
            }
            else
            {
                int result = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (result != 0) return result;
                i++;
                j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }

    // Compara cifras de cualquier longitud sin convertirlas (pueden no caber en un long).
    private static int CompareNumbers(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        a = a.TrimStart('0');
        b = b.TrimStart('0');
        if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
        return a.SequenceCompareTo(b);
    }
}
