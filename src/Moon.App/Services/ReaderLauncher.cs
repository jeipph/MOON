using System.IO;
using System.Windows;
using Moon.App.ViewModels;
using Moon.App.Windows;
using Moon.Core.Comics;
using Moon.Core.Library;
using Moon.Windows.Comics;

namespace Moon.App.Services;

/// <summary>
/// Abre cada libro en su propia ventana: los de texto en el lector de libros y los cómics,
/// el manga y los PDF en el lector de cómics. Si ya está abierto, lo trae al frente.
/// </summary>
public sealed class ReaderLauncher
{
    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly WebViewHost _webViewHost;
    private readonly Dictionary<long, Window> _open = [];

    public ReaderLauncher(LibraryService library, SettingsService settings, ThemeService themes, WebViewHost webViewHost)
    {
        _library = library;
        _settings = settings;
        _themes = themes;
        _webViewHost = webViewHost;
    }

    /// <summary>Al cerrar un libro (para que la biblioteca muestre el progreso nuevo).</summary>
    public event EventHandler? ReaderClosed;

    /// <returns>Null si se abrió; si no, el motivo para mostrarlo.</returns>
    public async Task<string?> OpenAsync(Book book, string filePath)
    {
        if (_open.TryGetValue(book.Id, out Window? existing))
        {
            existing.Activate();
            return null;
        }

        Window window;
        if (book.OpensAsBook)
        {
            var viewModel = new ReaderViewModel(book, filePath, _library, _settings, _themes);
            window = new ReaderWindow(viewModel, _webViewHost, _themes);
        }
        else if (book.OpensAsComic)
        {
            // Abrir el archivo y mirar si es manga toca el disco: en segundo plano.
            (IPageSource? source, bool isManga, string? error) = await Task.Run(() =>
            {
                try
                {
                    IPageSource pages = PdfPageSource.OpenAny(filePath, book.Format);
                    bool manga = book.Format != BookFormat.Pdf && book.ComicView is null && BookMetadata.IsManga(filePath);
                    return (pages, manga, (string?)null);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException) // archivo roto o protegido
                {
                    return ((IPageSource?)null, false, exception.Message);
                }
            });
            if (source is null)
                return $"No se pudo abrir \"{book.Title}\": {error}";
            if (_open.TryGetValue(book.Id, out existing)) // doble clic mientras se abría
            {
                source.Dispose();
                existing.Activate();
                return null;
            }
            var viewModel = new ComicReaderViewModel(book, new PageImages(source), _library, _settings, isManga);
            window = new ComicReaderWindow(viewModel, _themes);
        }
        else
        {
            return $"MOON todavía no sabe abrir archivos {BookFormats.Label(book.Format)}.";
        }

        _open[book.Id] = window;
        window.Closed += (_, _) =>
        {
            _open.Remove(book.Id);
            ReaderClosed?.Invoke(this, EventArgs.Empty);
        };
        window.Show();
        return null;
    }
}
