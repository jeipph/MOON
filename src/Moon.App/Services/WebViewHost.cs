using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Moon.App.Services;

/// <summary>
/// El "navegador" interno (WebView2) donde se muestran los libros. Todas las ventanas de lectura
/// comparten uno solo, con sus datos en <c>%LOCALAPPDATA%\MOON\WebView2</c> (sin cuentas ni historial).
/// </summary>
public sealed class WebViewHost
{
    /// <summary>Dirección interna de las páginas de MOON (carpeta Web junto al .exe).</summary>
    public const string AppHost = "app.moon";

    /// <summary>Dirección interna de la carpeta del libro abierto.</summary>
    public const string BookHost = "book.moon";

    private readonly string _userDataFolder;
    private Task<CoreWebView2Environment>? _environment;

    public WebViewHost(string userDataFolder) => _userDataFolder = userDataFolder;

    public static string DefaultUserDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOON", "WebView2");

    public static string WebFolder => Path.Combine(AppContext.BaseDirectory, "Web");

    public Task<CoreWebView2Environment> GetEnvironmentAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(null, _userDataFolder);
}
