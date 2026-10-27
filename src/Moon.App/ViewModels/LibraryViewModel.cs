using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Library;

namespace Moon.App.ViewModels;

/// <summary>
/// La biblioteca: carpetas a la izquierda, portadas a la derecha.
/// El trabajo con el disco (copiar, recorrer carpetas, leer portadas) va en segundo plano.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject, IDisposable
{
    private readonly LibraryService _library;
    private readonly LibraryWatcher _watcher;
    private readonly LibraryDialogs _dialogs;
    private readonly ReaderLauncher _reader;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly int _coverDecodeWidth;
    private List<BookItemViewModel> _allBooks = [];
    private bool _syncPending;

    public LibraryViewModel(LibraryService library, LibraryWatcher watcher, LibraryDialogs dialogs, ReaderLauncher reader)
    {
        _library = library;
        _watcher = watcher;
        _dialogs = dialogs;
        _reader = reader;
        _coverDecodeWidth = Application.Current.TryFindResource("Cover.DecodeWidth") as int? ?? 300;
        _watcher.Changed += (_, _) => Application.Current.Dispatcher.BeginInvoke(RequestSync);
        // Al cerrar un libro, su progreso y "Continuar leyendo" se ponen al día.
        _reader.ReaderClosed += (_, _) => Populate();
    }

    /// <summary>La biblioteca en sí (para ventanas como Vocabulario).</summary>
    public LibraryService Service => _library;

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    public ObservableCollection<BookItemViewModel> Books { get; } = [];

    [ObservableProperty]
    public partial SourceItemViewModel? SelectedSource { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Lo que se está haciendo ("Buscando libros…") o el resultado de lo último.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyLibrary), nameof(ShowNoResults))]
    public partial bool IsLibraryEmpty { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoResults))]
    public partial bool HasVisibleBooks { get; private set; }

    public bool ShowEmptyLibrary => IsLibraryEmpty;

    public bool ShowNoResults => !IsLibraryEmpty && !HasVisibleBooks;

    partial void OnSelectedSourceChanged(SourceItemViewModel? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>Al abrir MOON: muestra lo guardado y luego revisa las carpetas.</summary>
    public async Task StartAsync()
    {
        await Task.Run(() => { }); // deja pintar la ventana antes de tocar el disco
        RequestSync();
    }

    /// <summary>Revisa las carpetas. Si ya se está revisando, se repite al terminar.</summary>
    public async void RequestSync()
    {
        if (!await _busy.WaitAsync(0))
        {
            _syncPending = true;
            return;
        }
        try
        {
            do
            {
                _syncPending = false;
                await RunAsync(progress => _library.Sync(progress), result => Describe(result));
            }
            while (_syncPending);
        }
        finally
        {
            _busy.Release();
        }
    }

    [RelayCommand]
    private async Task AddBooks()
    {
        IReadOnlyList<string> files = _dialogs.PickBooks();
        if (files.Count > 0)
            await ImportAsync(files);
    }

    [RelayCommand]
    private async Task LinkFolder()
    {
        string? folder = _dialogs.PickFolder();
        if (folder is not null)
            await LinkAsync(folder);
    }

    /// <summary>Arrastrar y soltar: los archivos se añaden (copia) y las carpetas se enlazan.</summary>
    public async Task DropAsync(IReadOnlyList<string> paths)
    {
        var files = paths.Where(File.Exists).ToList();
        if (files.Count > 0)
            await ImportAsync(files);
        foreach (string folder in paths.Where(Directory.Exists))
            await LinkAsync(folder);
    }

    [RelayCommand]
    private async Task Unlink(SourceItemViewModel? item)
    {
        if (item?.Source is not { Kind: SourceKind.Linked } source)
            return;
        if (!_dialogs.Confirm($"¿Quitar \"{item.Name}\" de la biblioteca?\n\nSus {item.Count} libros dejarán de aparecer en MOON, pero los archivos NO se borran del disco."))
            return;
        await ExclusiveAsync(() => RunAsync(_ => _library.Unlink(source.Id), () => $"Se quitó \"{item.Name}\"."));
    }

    [RelayCommand]
    private async Task OpenBook(BookItemViewModel? book)
    {
        if (book is null)
            return;
        if (!book.IsAvailable)
        {
            Status = $"\"{book.Title}\" está en un disco desconectado. Conéctalo para leerlo.";
            return;
        }
        string? path = _library.FullPath(book.Book);
        if (path is null || !File.Exists(path))
        {
            Status = $"No se encuentra el archivo de \"{book.Title}\". MOON revisará la carpeta en unos segundos.";
            RequestSync();
            return;
        }
        // Se usa el libro recién leído de la base de datos (con su última página y ajustes).
        Book current = _library.Books.FirstOrDefault(b => b.Id == book.Book.Id) ?? book.Book;
        string? error = await _reader.OpenAsync(current, path);
        if (error is not null)
            Status = error;
    }

    [RelayCommand]
    private void ShowInExplorer(BookItemViewModel? book)
    {
        string? path = book is null ? null : _library.FullPath(book.Book);
        if (path is not null && File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    [RelayCommand]
    private async Task RemoveBook(BookItemViewModel? book)
    {
        if (book is null)
            return;
        bool managed = _library.Sources.FirstOrDefault(s => s.Id == book.Book.SourceId)?.Kind == SourceKind.Managed;
        string warning = managed
            ? "Se borrará la copia guardada en la carpeta de MOON."
            : "El archivo NO se borra: sigue en su carpeta. Volverá a aparecer si quitas y vuelves a enlazar la carpeta.";
        if (!_dialogs.Confirm($"¿Quitar \"{book.Title}\" de la biblioteca?\n\n{warning}"))
            return;
        await ExclusiveAsync(() => RunAsync(_ => _library.Remove(book.Book), () => $"Se quitó \"{book.Title}\"."));
    }

    private Task ImportAsync(IReadOnlyList<string> files) => ExclusiveAsync(() =>
        RunAsync(progress => _library.Import(files, progress), result =>
        {
            var parts = new List<string> { result.Added == 1 ? "1 libro añadido" : $"{result.Added} libros añadidos" };
            if (result.AlreadyInLibrary > 0) parts.Add($"{result.AlreadyInLibrary} ya estaban");
            if (result.NotBooks > 0) parts.Add($"{result.NotBooks} no son libros");
            return string.Join(" · ", parts) + ".";
        }));

    private Task LinkAsync(string folder) => ExclusiveAsync(() =>
        RunAsync(progress => _library.Link(folder, progress), result => result switch
        {
            LinkResult.Linked => $"Carpeta enlazada: {folder}",
            LinkResult.AlreadyLinked => "Esa carpeta ya está en la biblioteca.",
            LinkResult.ContainsLinkedFolder => "Dentro de esa carpeta hay otra que ya está enlazada. Quítala primero y enlaza la de fuera.",
            _ => "No se encontró la carpeta.",
        }));

    // Una operación a la vez (no se puede copiar libros mientras se recorre el disco).
    private async Task ExclusiveAsync(Func<Task> action)
    {
        await _busy.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _busy.Release();
        }
        if (_syncPending)
            RequestSync();
    }

    private Task RunAsync(Action<IProgress<string>> work, Func<string> describe) =>
        RunAsync(progress => { work(progress); return 0; }, _ => describe());

    private async Task RunAsync<T>(Func<IProgress<string>, T> work, Func<T, string> describe)
    {
        var progress = new Progress<string>(message => Status = message);
        try
        {
            T result = await Task.Run(() => work(progress));
            Status = describe(result);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Nunca dejar la biblioteca a medias y el estado congelado: se avisa y se sigue.
            Status = $"No se pudo completar: {exception.Message}";
        }
        Populate();
    }

    private static string Describe(SyncResult result)
    {
        var parts = new List<string>();
        if (result.Added > 0) parts.Add(result.Added == 1 ? "1 libro nuevo" : $"{result.Added} libros nuevos");
        if (result.Moved > 0) parts.Add(result.Moved == 1 ? "1 movido" : $"{result.Moved} movidos");
        if (result.Removed > 0) parts.Add(result.Removed == 1 ? "1 ya no está" : $"{result.Removed} ya no están");
        if (result.DisconnectedSources > 0) parts.Add(result.DisconnectedSources == 1 ? "1 carpeta desconectada" : $"{result.DisconnectedSources} carpetas desconectadas");
        return parts.Count == 0 ? "" : string.Join(" · ", parts) + ".";
    }

    // Vuelve a leer la biblioteca guardada y pinta la barra lateral y las portadas.
    private void Populate()
    {
        IReadOnlyDictionary<long, string?> paths = _library.CurrentPaths;
        IReadOnlyList<Book> books = _library.Books;
        IReadOnlyList<LibrarySource> sources = _library.Sources;

        _allBooks = books
            .Select(b => new BookItemViewModel(b, paths.GetValueOrDefault(b.SourceId) is not null, _library.CoverPath(b.Id), _coverDecodeWidth))
            .ToList();

        // Se recuerda qué fila estaba elegida para no perderla al repintar.
        SourceItemViewModel? previous = SelectedSource;
        int started = LibraryService.ContinueReading(books).Count();
        Sources.Clear();
        if (started > 0)
            Sources.Add(SourceItemViewModel.ContinueReading(started));
        SourceItemViewModel all = SourceItemViewModel.All(books.Count);
        Sources.Add(all);
        foreach (LibrarySource source in sources)
            Sources.Add(SourceItemViewModel.From(source, paths.GetValueOrDefault(source.Id), books.Count(b => b.SourceId == source.Id)));
        SelectedSource = previous switch
        {
            { IsContinueReading: true } => Sources.FirstOrDefault(s => s.IsContinueReading) ?? all,
            { Source: { } source } => Sources.FirstOrDefault(s => s.Source?.Id == source.Id) ?? all,
            _ => all,
        };

        IsLibraryEmpty = books.Count == 0;
        _watcher.Watch(paths.Values.OfType<string>());
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        long? sourceId = SelectedSource?.Source?.Id;
        string search = SearchText.Trim();
        IEnumerable<BookItemViewModel> pool = _allBooks;
        if (SelectedSource?.IsContinueReading == true)
        {
            var order = LibraryService.ContinueReading(_allBooks.Select(b => b.Book)).Select(b => b.Id).ToList();
            pool = order.Select(id => _allBooks.First(b => b.Book.Id == id));
        }
        var visible = pool.Where(b =>
            (sourceId is null || b.Book.SourceId == sourceId) &&
            (search.Length == 0 ||
             b.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
             (b.Author?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
             (b.Book.Series?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)));

        Books.Clear();
        foreach (BookItemViewModel book in visible)
            Books.Add(book);
        HasVisibleBooks = Books.Count > 0;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _busy.Dispose();
    }
}
