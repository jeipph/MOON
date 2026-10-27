using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Moon.App.Services;
using Moon.App.ViewModels;
using Moon.App.Windows;
using Moon.Core.Abstractions;
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
        _singleInstance = new Mutex(true, InstanceName, out bool isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
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

        _services = ConfigureServices();
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        ListenForShowSignal(window);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore());
        services.AddSingleton<ThemeService>();
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
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOON", "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch (Exception)
        {
            // Si ni siquiera se puede escribir el registro, al menos se ve el mensaje.
        }
        MessageBox.Show($"MOON encontró un error:\n\n{exception.Message}", "MOON", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<ThemeService>()?.Dispose();
        _services?.Dispose();
        _singleInstance?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }
}
