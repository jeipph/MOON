using System.Windows;
using System.Windows.Media;
using Moon.Core.Annotations;

namespace Moon.App.ViewModels;

/// <summary>Una fila del panel de notas: subrayado, marcador o zona de cómic.</summary>
public sealed class AnnotationItemViewModel(Annotation annotation)
{
    public Annotation Annotation { get; } = annotation;

    public bool IsBookmark => Annotation.Kind == AnnotationKind.Bookmark;

    /// <summary>Lo subrayado; en marcadores y zonas, una descripción.</summary>
    public string Text => Annotation.Kind switch
    {
        AnnotationKind.Highlight => Annotation.Text,
        AnnotationKind.Bookmark => "Marcador",
        _ => "Zona marcada",
    };

    public string Note => Annotation.Note;

    public bool HasNote => !string.IsNullOrWhiteSpace(Annotation.Note);

    public string Where
    {
        get
        {
            string percent = $"{Math.Round(Annotation.Progress * 100)} %";
            return string.IsNullOrWhiteSpace(Annotation.Chapter) ? percent : $"{Annotation.Chapter} · {percent}";
        }
    }

    /// <summary>Color del subrayado, tomado de los tokens (Brush.Highlight.*).</summary>
    public Brush ColorBrush =>
        Application.Current.TryFindResource($"Brush.Highlight.{Annotation.Color}") as Brush ?? Brushes.Gold;
}
