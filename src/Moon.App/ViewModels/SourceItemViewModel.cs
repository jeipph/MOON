using System.IO;
using Moon.Core.Library;

namespace Moon.App.ViewModels;

/// <summary>
/// Una fila de la barra lateral: "Continuar leyendo", "Todos los libros", la carpeta de MOON
/// o una carpeta enlazada.
/// </summary>
public sealed class SourceItemViewModel
{
    private SourceItemViewModel(LibrarySource? source, string name, string detail, string icon, int count, bool isConnected, bool isContinueReading = false)
    {
        Source = source;
        Name = name;
        Detail = detail;
        Icon = icon;
        Count = count;
        IsConnected = isConnected;
        IsContinueReading = isContinueReading;
    }

    /// <summary>Null = "Todos los libros" o "Continuar leyendo".</summary>
    public LibrarySource? Source { get; }

    /// <summary>Muestra solo los libros empezados, el último leído primero.</summary>
    public bool IsContinueReading { get; }

    public string Name { get; }

    public string Detail { get; }

    /// <summary>Carácter de Segoe Fluent Icons.</summary>
    public string Icon { get; }

    public int Count { get; }

    public bool IsConnected { get; }

    public bool CanUnlink => Source?.Kind == SourceKind.Linked;

    public static SourceItemViewModel ContinueReading(int count) =>
        new(null, "Continuar leyendo", count == 1 ? "1 libro empezado" : $"{count} libros empezados", "", count, true, isContinueReading: true);

    public static SourceItemViewModel All(int count) =>
        new(null, "Todos los libros", $"{count} en total", "\uE71D", count, true);

    public static SourceItemViewModel From(LibrarySource source, string? currentPath, int count)
    {
        bool connected = currentPath is not null;
        if (source.Kind == SourceKind.Managed)
            return new(source, "Biblioteca de MOON", count == 1 ? "1 libro" : $"{count} libros", "\uE82D", count, true);

        string path = currentPath ?? source.Path;
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (string.IsNullOrEmpty(name))
            name = source.VolumeLabel ?? path; // carpeta raíz de un disco: "E:\"
        string detail = connected
            ? $"{path} · {count}"
            : $"Desconectado{(source.VolumeLabel is null ? "" : $" ({source.VolumeLabel})")} · {count}";
        return new(source, name, detail, connected ? "\uEDA2" : "\uE8CD", count, connected);
    }
}
