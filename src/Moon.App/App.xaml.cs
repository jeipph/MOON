using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.App.Windows;
using Moon.Core.Abstractions;
using Moon.Core.Library;
using Moon.Core.Settings;

namespace Moon.App;

public partial class App : Application
{
    // Evita abrir dos MOON a la vez; el segundo avisa al primero para que se muestre.
    private const string InstanceName = @"Local\MOON.SingleInstance";
    private const string ShowSignalName = @"Local\MOON.Show";

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Con --datos es una copia de prueba: no choca con el MOON de verdad.
        string? dataFolder = DataFolderFromArgs(e.Args);
        string suffix = dataFolder is null ? "" : ".Prueba";
        _singleInstance = new Mutex(true, InstanceName + suffix, out bool isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName + suffix);
        if (!isFirst)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Un error no previsto se muestra y se apunta, en vez de perderse en silencio.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportError(args.Exception);
        };

        _services = ConfigureServices(dataFolder);
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        ListenForShowSignal(window);
        _ = _services.GetRequiredService<LibraryViewModel>().StartAsync();
    }

    /// <summary>
    /// "--datos C:\carpeta": todo (ajustes, base de datos, copias y portadas) va a esa carpeta.
    /// Sirve para probar MOON sin tocar la biblioteca de verdad.
    /// </summary>
    private static string? DataFolderFromArgs(string[] args)
    {
        int index = Array.FindIndex(args, a => a.Equals("--datos", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : null;
    }

    private static ServiceProvider ConfigureServices(string? dataFolder)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsStore>(_ =>
            new JsonSettingsStore(dataFolder is null ? null : Path.Combine(dataFolder, "settings.json")));
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton(_ => new WebViewHost(
            dataFolder is null ? WebViewHost.DefaultUserDataFolder : Path.Combine(dataFolder, "WebView2")));
        services.AddSingleton<DictionaryService>();
        services.AddSingleton<VoiceService>();
        services.AddSingleton<Moon.Core.Ocr.IOcrEngine, Moon.Windows.Ocr.WindowsOcrEngine>();
        services.AddSingleton<ReaderLauncher>();
        services.AddSingleton(_ => new LibraryService(
            new SqliteLibraryStore(dataFolder is null ? SqliteLibraryStore.DefaultPath : Path.Combine(dataFolder, "moon.db")),
            dataFolder is null ? LibraryService.DefaultManagedFolder : Path.Combine(dataFolder, "Biblioteca"),
            dataFolder is null ? LibraryService.DefaultCoversFolder : Path.Combine(dataFolder, "covers"),
            LibraryService.CurrentDrives,
            Moon.Windows.Comics.PdfPageSource.ReadMetadata)); // portada de los PDF
        services.AddSingleton<LibraryWatcher>();
        services.AddSingleton<LibraryDialogs>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    // Espera en segundo plano a que otra copia de MOON pida mostrar la ventana.
    private void ListenForShowSignal(MainWindow window)
    {
        EventWaitHandle signal = _showSignal!;
        var thread = new Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.BeginInvoke(window.BringToFront);
        })
        { IsBackground = true, Name = "MOON.ShowSignal" };
        thread.Start();
    }

    private static void ReportError(Exception exception)
    {
        ErrorLog.Write(exception.ToString());
        MessageBox.Show($"MOON encontró un error:\n\n{exception.Message}", "MOON", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<ThemeService>()?.Dispose();
        _services?.GetService<LibraryViewModel>()?.Dispose();
        _services?.GetService<VoiceService>()?.Dispose(); // cierra el motor de voz
        _services?.Dispose();
        _singleInstance?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }
}
