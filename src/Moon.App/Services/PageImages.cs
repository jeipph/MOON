using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moon.Core.Comics;

namespace Moon.App.Services;

/// <summary>
/// Carga las páginas de un cómic como imágenes listas para mostrar: en segundo plano, al tamaño
/// justo (no a 4000 px si la pantalla muestra 1200) y con los ajustes de imagen aplicados.
/// Guarda unas pocas páginas recientes para que pasar adelante y atrás sea instantáneo.
/// </summary>
public sealed class PageImages : IDisposable
{
    private const int CacheSize = 8;
    // Antes de saber su tamaño real, se supone una página vertical normal (alto = 1,5 × ancho).
    private const double DefaultAspect = 1.5;

    private readonly IPageSource _source;
    private readonly (double Width, double Height)?[] _sizes;
    private readonly LinkedList<(Key Key, BitmapSource Image)> _cache = new();
    private readonly Lock _cacheLock = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly SemaphoreSlim _decoders = new(2, 2);

    private readonly record struct Key(int Page, int Width, ImageAdjustments Adjustments);

    public PageImages(IPageSource source)
    {
        _source = source;
        _sizes = new (double, double)?[source.Count];
        if (source is IKnownPageSizes known)
        {
            for (int i = 0; i < source.Count; i++)
                _sizes[i] = known.PageSize(i);
        }
    }

    public int Count => _source.Count;

    /// <summary>Se avisa cuando ya se conocen los tamaños de todas las páginas (desde otro hilo).</summary>
    public event EventHandler? SizesKnown;

    /// <summary>Alto ÷ ancho de la página (1,5 si aún no se sabe).</summary>
    public double Aspect(int page) => _sizes[page] is { } s && s.Width > 0 ? s.Height / s.Width : DefaultAspect;

    /// <summary>Página panorámica (más ancha que alta): en doble página va sola.</summary>
    public bool IsWide(int page) => _sizes[page] is { } s && s.Width > s.Height * 1.1;

    /// <summary>Mide todas las páginas leyendo solo su cabecera (en segundo plano).</summary>
    public void MeasureAllInBackground()
    {
        if (_sizes.All(s => s is not null))
        {
            SizesKnown?.Invoke(this, EventArgs.Empty);
            return;
        }
        CancellationToken token = _closing.Token;
        Task.Run(() =>
        {
            for (int i = 0; i < _sizes.Length && !token.IsCancellationRequested; i++)
            {
                if (_sizes[i] is not null)
                    continue;
                try
                {
                    using var stream = new MemoryStream(_source.Read(i, 0));
                    BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
                    _sizes[i] = (frame.PixelWidth, frame.PixelHeight);
                }
                catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidDataException or ArgumentException or FileFormatException or ObjectDisposedException or InvalidOperationException)
                {
                    _sizes[i] = (1, DefaultAspect); // página dañada: se trata como vertical
                }
            }
            if (!token.IsCancellationRequested)
                SizesKnown?.Invoke(this, EventArgs.Empty);
        }, token);
    }

    /// <summary>La página al ancho pedido (en píxeles de pantalla), o null si está dañada.</summary>
    /// <param name="stillWanted">Si se indica, se pregunta justo antes de decodificar (¿sigue en pantalla?).</param>
    public Task<BitmapSource?> GetAsync(int page, int width, ImageAdjustments adjustments, Func<bool>? stillWanted = null)
    {
        // Se redondea el ancho para no recargar por cambios de 1 px al mover la ventana.
        width = Math.Max(64, (int)Math.Ceiling(width / 64.0) * 64);
        var key = new Key(page, width, adjustments);
        lock (_cacheLock)
        {
            LinkedListNode<(Key Key, BitmapSource Image)>? node = _cache.First;
            while (node is not null && node.Value.Key != key)
                node = node.Next;
            if (node is not null)
            {
                _cache.Remove(node);
                _cache.AddFirst(node);
                return Task.FromResult<BitmapSource?>(node.Value.Image);
            }
        }
        CancellationToken token = _closing.Token;
        // Sin pasar el token a Task.Run: si la ventana se cierra, la tarea devuelve null en vez de
        // "cancelarse" (una tarea cancelada lanzaría una excepción en quien la espera).
        return Task.Run(async () =>
        {
            // Como mucho dos páginas a la vez: al bajar rápido en webtoon no se amontonan decenas.
            try
            {
                await _decoders.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return null; // se cerró la ventana
            }
            try
            {
                // Si mientras esperaba la página ya salió de la pantalla, no se gasta memoria en ella.
                if (token.IsCancellationRequested || (stillWanted is not null && !stillWanted()))
                    return null;
                BitmapSource? image = Decode(page, width, adjustments);
                if (image is not null)
                {
                    lock (_cacheLock)
                    {
                        _cache.AddFirst((key, image));
                        while (_cache.Count > CacheSize)
                            _cache.RemoveLast(); // las páginas lejanas se sueltan de la memoria
                    }
                }
                return image;
            }
            finally
            {
                _decoders.Release();
            }
        });
    }

    private BitmapSource? Decode(int page, int width, ImageAdjustments adjustments)
    {
        try
        {
            byte[] bytes = _source.Read(page, width);
            // Primero solo la cabecera (cuánto mide) y luego una sola decodificación al tamaño justo.
            // Decodificar a tamaño completo para medir gastaba ~24 MB por página de 2000×3000.
            BitmapFrame header = BitmapDecoder.Create(new MemoryStream(bytes),
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
            _sizes[page] ??= (header.PixelWidth, header.PixelHeight);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (header.PixelWidth > width)
                image.DecodePixelWidth = width; // solo se reduce: agrandar una página pequeña no la mejora
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return adjustments.IsNone ? image : Adjust(image, adjustments);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidDataException or ArgumentException or FileFormatException or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }
    }

    // Ajustes de imagen con una tabla por color: rápido incluso en páginas grandes.
    private static BitmapSource Adjust(BitmapSource source, ImageAdjustments adjustments)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);

        byte[][] tables = new byte[3][];
        if (adjustments.Whiten)
        {
            var histograms = new int[3][] { new int[256], new int[256], new int[256] };
            for (int i = 0; i < pixels.Length; i += 4)
            {
                histograms[0][pixels[i]]++;
                histograms[1][pixels[i + 1]]++;
                histograms[2][pixels[i + 2]]++;
            }
            for (int c = 0; c < 3; c++)
            {
                (byte low, byte high) = ImageAdjustments.Levels(histograms[c]);
                tables[c] = adjustments.BuildTable(low, high);
            }
        }
        else
        {
            byte[] table = adjustments.BuildTable();
            tables[0] = tables[1] = tables[2] = table;
        }

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = tables[0][pixels[i]];         // azul
            pixels[i + 1] = tables[1][pixels[i + 1]]; // verde
            pixels[i + 2] = tables[2][pixels[i + 2]]; // rojo
        }
        BitmapSource result = BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    public void Dispose()
    {
        _closing.Cancel();
        lock (_cacheLock)
            _cache.Clear();
        _source.Dispose();
        _closing.Dispose();
    }
}
