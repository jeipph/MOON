using System.IO;
using System.Text;
using System.Windows;
using Moon.Core.Dictionary;
using Moon.Core.Library;

namespace Moon.App.Windows;

/// <summary>Las palabras guardadas al leer: ver, borrar y exportar (Anki o CSV).</summary>
public partial class VocabularyWindow : Window
{
    private readonly LibraryService _library;

    public VocabularyWindow(Window owner, LibraryService library)
    {
        InitializeComponent();
        Owner = owner;
        _library = library;
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<VocabularyEntry> words = _library.Vocabulary;
        WordList.ItemsSource = words;
        CountText.Text = words.Count == 1 ? "1 palabra" : $"{words.Count} palabras";
        EmptyText.Visibility = words.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (WordList.SelectedItem is VocabularyEntry entry)
        {
            _library.DeleteVocabulary(entry.Id);
            Refresh();
        }
    }

    private void OnExportAnkiClick(object sender, RoutedEventArgs e) =>
        Export("Texto para Anki|*.txt", "vocabulario-anki.txt", VocabularyExporter.ToAnki);

    private void OnExportCsvClick(object sender, RoutedEventArgs e) =>
        Export("CSV (Excel)|*.csv", "vocabulario.csv", ToCsv);

    private void Export(string filter, string fileName, Func<IEnumerable<VocabularyEntry>, string> format)
    {
        IReadOnlyList<VocabularyEntry> words = _library.Vocabulary;
        if (words.Count == 0)
            return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Exportar vocabulario", Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) == true)
            File.WriteAllText(dialog.FileName, format(words), new UTF8Encoding(true));
    }

    // Punto y coma para Excel en español.
    private static string ToCsv(IEnumerable<VocabularyEntry> words)
    {
        static string Cell(string text) => text.IndexOfAny([';', '"', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        var csv = new StringBuilder("Palabra;Forma base;Traducción;Frase;Libro;Fecha\n");
        foreach (VocabularyEntry w in words)
            csv.AppendLine(string.Join(';', Cell(w.Word), Cell(w.Lemma), Cell(w.Translation), Cell(w.Context), Cell(w.BookTitle), w.CreatedAt.ToString("yyyy-MM-dd")));
        return csv.ToString();
    }
}
