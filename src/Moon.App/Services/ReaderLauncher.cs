using Moon.App.ViewModels;
using Moon.App.Windows;
using Moon.Core.Library;

namespace Moon.App.Services;

/// <summary>Abre cada libro en su propia ventana; si ya está abierto, la trae al frente.</summary>
public sealed class ReaderLauncher
{
    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly WebViewHost _webViewHost;
    private readonly Dictionary<long, ReaderWindow> _open = [];

    public ReaderLauncher(LibraryService library, SettingsService settings, ThemeService themes, WebViewHost webViewHost)
    {
        _library = library;
        _settings = settings;
        _themes = themes;
        _webViewHost = webViewHost;
    }

    /// <summary>Al cerrar un libro (para que la biblioteca muestre el progreso nuevo).</summary>
    public event EventHandler? ReaderClosed;

    public void Open(Book book, string filePath)
    {
        if (_open.TryGetValue(book.Id, out ReaderWindow? existing))
        {
            existing.Activate();
            return;
        }
        var viewModel = new ReaderViewModel(book, filePath, _library, _settings, _themes);
        var window = new ReaderWindow(viewModel, _webViewHost, _themes);
        _open[book.Id] = window;
        window.Closed += (_, _) =>
        {
            _open.Remove(book.Id);
            ReaderClosed?.Invoke(this, EventArgs.Empty);
        };
        window.Show();
    }
}
