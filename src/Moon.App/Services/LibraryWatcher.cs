using System.IO;

namespace Moon.App.Services;

/// <summary>
/// Vigila las carpetas de la biblioteca y avisa (una sola vez, tras 2 s de calma) cuando
/// se añaden, borran o renombran archivos. Así no hay que revisar las carpetas cada cierto tiempo.
/// </summary>
public sealed class LibraryWatcher : IDisposable
{
    private static readonly TimeSpan Calm = TimeSpan.FromSeconds(2);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Timer _timer;

    public LibraryWatcher()
    {
        _timer = new Timer(_ => Changed?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Algo cambió en alguna carpeta. Llega desde otro hilo.</summary>
    public event EventHandler? Changed;

    public void Watch(IEnumerable<string> folders)
    {
        Clear();
        foreach (string folder in folders)
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                watcher.Created += OnChange;
                watcher.Deleted += OnChange;
                watcher.Renamed += OnChange;
                watcher.Changed += OnChange;
                // Si se pierde la vigilancia (p. ej. se desenchufa el disco), también hay que revisar.
                watcher.Error += (_, _) => Restart();
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Carpeta que ya no existe o sin permiso: se volverá a intentar en la próxima revisión.
            }
        }
    }

    private void OnChange(object sender, FileSystemEventArgs e) => Restart();

    // Copiar 200 libros produce cientos de avisos: se espera a que pare para revisar una vez.
    private void Restart() => _timer.Change(Calm, Timeout.InfiniteTimeSpan);

    private void Clear()
    {
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
    }

    public void Dispose()
    {
        Clear();
        _timer.Dispose();
    }
}
