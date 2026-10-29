using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Moon.Core.Library;

namespace Moon.App.ViewModels;

/// <summary>Un libro en la cuadrícula: portada (cargada en segundo plano), título, autor y progreso.</summary>
public sealed partial class BookItemViewModel : ObservableObject
{
    private readonly string _coverPath;
    private readonly int _decodeWidth;
    private bool _coverRequested;
    private bool _coverFailed;

    public BookItemViewModel(Book book, bool isAvailable, string coverPath, int decodeWidth)
    {
        Book = book;
        IsAvailable = isAvailable;
        _coverPath = coverPath;
        _decodeWidth = decodeWidth;
    }

    public Book Book { get; }

    public string Title => Book.Title;

    public string? Author => Book.Author;

    public string FormatLabel => BookFormats.Label(Book.Format);

    public double Progress => Book.Progress;

    public bool HasProgress => Book.Progress > 0;

    public bool IsPdf => Book.Format == BookFormat.Pdf;

    /// <summary>En el menú de un PDF: abrirlo en el otro modo (y recordarlo).</summary>
    public string PdfModeMenuHeader => Book.IsPdfDocument ? "Abrir como imágenes (manga)" : "Abrir como documento (texto)";

    /// <summary>False si el libro está en un disco desconectado.</summary>
    public bool IsAvailable { get; }

    [ObservableProperty]
    public partial ImageSource? Cover { get; private set; }

    /// <summary>Sin portada (o dañada): se dibuja una con el título.</summary>
    public bool HasNoCover => !Book.HasCover || _coverFailed;

    /// <summary>La portada se lee la primera vez que se necesita, sin frenar la ventana.</summary>
    public void EnsureCover()
    {
        if (_coverRequested || !Book.HasCover)
            return;
        _coverRequested = true;
        Task.Run(() =>
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // lee y suelta el archivo
                image.DecodePixelWidth = _decodeWidth;          // no guarda la imagen a tamaño completo
                image.UriSource = new Uri(_coverPath);
                image.EndInit();
                image.Freeze();
                return (ImageSource)image;
            }
            catch (Exception exception) when (exception is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
            {
                return null; // portada dañada o en un formato que Windows no sabe leer
            }
        }).ContinueWith(task =>
        {
            Cover = task.Result;
            if (Cover is null)
            {
                _coverFailed = true;
                OnPropertyChanged(nameof(HasNoCover));
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
