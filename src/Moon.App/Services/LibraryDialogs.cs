using System.Windows;
using Microsoft.Win32;

namespace Moon.App.Services;

/// <summary>Ventanas de Windows para elegir archivos y carpetas, y preguntas de confirmación.</summary>
public sealed class LibraryDialogs
{
    private const string BookFilter =
        "Libros y cómics|*.epub;*.pdf;*.cbz;*.cbr;*.cb7;*.mobi;*.azw3;*.fb2;*.txt|Todos los archivos|*.*";

    public IReadOnlyList<string> PickBooks()
    {
        var dialog = new OpenFileDialog { Title = "Añadir libros a MOON", Filter = BookFilter, Multiselect = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : [];
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Enlazar una carpeta de libros (también de un disco externo)" };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "MOON", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Inform(string message) =>
        MessageBox.Show(Application.Current.MainWindow, message, "MOON", MessageBoxButton.OK, MessageBoxImage.Information);
}
