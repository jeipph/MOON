namespace Moon.Core.Comics;

/// <summary>
/// Ajustes de imagen para escaneos (sin tocar el archivo original).
/// Brillo y contraste de −100 a 100, gamma en % (100 = sin cambio).
/// "Quitar amarillo" estira cada color para que el papel viejo vuelva a ser blanco.
/// </summary>
public sealed record ImageAdjustments(int Brightness = 0, int Contrast = 0, int Gamma = 100, bool Whiten = false)
{
    public static readonly ImageAdjustments None = new();

    public bool IsNone => this == None;

    /// <summary>
    /// Tabla de 256 valores: el color c de un píxel pasa a ser <c>tabla[c]</c>.
    /// <paramref name="low"/> y <paramref name="high"/> son los niveles de negro y blanco
    /// (para "quitar amarillo"; 0 y 255 = no estirar).
    /// </summary>
    public byte[] BuildTable(byte low = 0, byte high = 255)
    {
        var table = new byte[256];
        double range = Math.Max(1, high - low);
        double contrast = (100 + Math.Clamp(Contrast, -100, 100)) / 100.0; // 0 … 2
        double brightness = Math.Clamp(Brightness, -100, 100) / 100.0;     // −1 … 1
        double gamma = 100.0 / Math.Clamp(Gamma, 20, 300);
        for (int c = 0; c < 256; c++)
        {
            double v = Math.Clamp((c - low) / range, 0, 1); // niveles
            v = Math.Pow(v, gamma);                         // gamma: >100 % aclara los tonos medios
            v = (v - 0.5) * contrast + 0.5;                 // contraste alrededor del gris medio
            v += brightness;                                // brillo
            table[c] = (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        }
        return table;
    }

    /// <summary>
    /// Niveles de negro y blanco de un color a partir de su histograma: se ignora el 0,5 %
    /// más oscuro y el 0,5 % más claro (manchas, motas) para no dejarse engañar por ellos.
    /// </summary>
    public static (byte Low, byte High) Levels(int[] histogram)
    {
        long total = histogram.Sum(h => (long)h);
        if (total == 0)
            return (0, 255);
        long cut = (long)(total * 0.005);
        int low = 0, high = 255;
        for (long seen = 0; low < 255 && (seen += histogram[low]) <= cut; low++) { }
        for (long seen = 0; high > 0 && (seen += histogram[high]) <= cut; high--) { }
        if (high - low < 32)
            return (0, 255); // imagen casi plana: no se estira
        return ((byte)low, (byte)high);
    }
}
