using System.Runtime.InteropServices.WindowsRuntime;
using Moon.Core.Comics;
using Moon.Core.Library;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Moon.Windows.Comics;

/// <summary>
/// Un PDF visto como páginas de imagen, con el lector de PDF que trae Windows (sin descargas).
/// Ideal para manga y cómics escaneados; seleccionar texto llegará con el visor de PDF (L10).
/// </summary>
public sealed class PdfPageSource : IPageSource, IKnownPageSizes
{
    // Ancho al que se dibuja si no se pide otro (suficiente para una pantalla grande).
    private const int DefaultWidth = 1600;

    private readonly PdfDocument _document;
    private readonly (double Width, double Height)[] _sizes;
    private readonly Lock _lock = new();

    private PdfPageSource(PdfDocument document)
    {
        _document = document;
        _sizes = new (double, double)[document.PageCount];
        for (uint i = 0; i < document.PageCount; i++)
        {
            using PdfPage page = document.GetPage(i);
            _sizes[i] = (page.Size.Width, page.Size.Height);
        }
    }

    /// <exception cref="InvalidDataException">PDF dañado o protegido con contraseña.</exception>
    public static PdfPageSource Open(string path)
    {
        try
        {
            StorageFile file = StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
            PdfDocument document = PdfDocument.LoadFromFileAsync(file).AsTask().GetAwaiter().GetResult();
            if (document.PageCount == 0)
                throw new InvalidDataException("El PDF no tiene páginas.");
            return new PdfPageSource(document);
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException(
                exception.HResult == unchecked((int)0x8007052B)
                    ? "El PDF está protegido con contraseña."
                    : "No se pudo abrir el PDF (¿dañado?).", exception);
        }
    }

    public int Count => _sizes.Length;

    public string Name(int index) => $"Página {index + 1}";

    public (double Width, double Height) PageSize(int index) => _sizes[index];

    public byte[] Read(int index, int targetWidth)
    {
        lock (_lock) // el PDF se dibuja de una página en una
        {
            using PdfPage page = _document.GetPage((uint)index);
            using var stream = new InMemoryRandomAccessStream();
            var options = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Clamp(targetWidth > 0 ? targetWidth : DefaultWidth, 200, 4000) };
            page.RenderToStreamAsync(stream, options).AsTask().GetAwaiter().GetResult();
            var bytes = new byte[stream.Size];
            stream.Seek(0);
            stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None).AsTask().GetAwaiter().GetResult();
            return bytes;
        }
    }

    public void Dispose() { } // PdfDocument no necesita cerrarse

    /// <summary>Portada para la biblioteca: la primera página, pequeña.</summary>
    public static BookMetadata? ReadMetadata(string path, BookFormat format)
    {
        if (format != BookFormat.Pdf)
            return null;
        try
        {
            using PdfPageSource pdf = Open(path);
            BookMetadata fromName = BookMetadata.FromFileName(path);
            return fromName with { Cover = pdf.Read(0, 400) };
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Abre cualquier cómic: PDF con Windows; CBZ, CBR, CB7 y carpetas con <see cref="PageSources"/>.</summary>
    public static IPageSource OpenAny(string path, BookFormat format) =>
        format == BookFormat.Pdf ? Open(path) : PageSources.OpenArchive(path);
}
