using System.Globalization;
using System.Text;
using Moon.Core.Library;

namespace Moon.Core.Annotations;

/// <summary>
/// Exporta las notas de un libro a Markdown (para Obsidian u otras apps de notas) o a CSV (Excel).
/// </summary>
public static class AnnotationExporter
{
    public static string ToMarkdown(Book book, IEnumerable<Annotation> annotations)
    {
        var md = new StringBuilder();
        md.AppendLine("---");
        md.AppendLine($"titulo: \"{Yaml(book.Title)}\"");
        if (!string.IsNullOrWhiteSpace(book.Author))
            md.AppendLine($"autor: \"{Yaml(book.Author)}\"");
        md.AppendLine($"exportado: {DateTime.Now:yyyy-MM-dd}");
        md.AppendLine("fuente: MOON");
        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine($"# {book.Title}");
        if (!string.IsNullOrWhiteSpace(book.Author))
            md.AppendLine($"*{book.Author}*");

        string? chapter = null;
        foreach (Annotation a in Ordered(annotations))
        {
            if (a.Chapter != chapter && !string.IsNullOrWhiteSpace(a.Chapter))
            {
                chapter = a.Chapter;
                md.AppendLine();
                md.AppendLine($"## {chapter}");
            }
            md.AppendLine();
            switch (a.Kind)
            {
                case AnnotationKind.Highlight:
                    foreach (string line in a.Text.Trim().Split('\n'))
                        md.AppendLine($"> {line.Trim()}");
                    break;
                case AnnotationKind.Bookmark:
                    md.AppendLine($"🔖 Marcador ({Percent(a.Progress)})");
                    break;
                case AnnotationKind.Region:
                    md.AppendLine($"▢ Zona marcada en {a.Chapter}");
                    break;
            }
            if (!string.IsNullOrWhiteSpace(a.Note))
            {
                md.AppendLine();
                md.AppendLine(a.Note.Trim());
            }
        }
        return md.ToString();
    }

    /// <summary>CSV con punto y coma (Excel en español lo abre bien) y BOM para las tildes.</summary>
    public static string ToCsv(Book book, IEnumerable<Annotation> annotations)
    {
        var csv = new StringBuilder("﻿");
        csv.AppendLine("Libro;Tipo;Capítulo;Posición;Texto;Nota;Color;Fecha");
        foreach (Annotation a in Ordered(annotations))
        {
            string kind = a.Kind switch
            {
                AnnotationKind.Highlight => "Subrayado",
                AnnotationKind.Bookmark => "Marcador",
                _ => "Zona",
            };
            csv.AppendLine(string.Join(';',
                Cell(book.Title), kind, Cell(a.Chapter), Percent(a.Progress), Cell(a.Text), Cell(a.Note),
                a.Color.ToString(), a.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        }
        return csv.ToString();
    }

    /// <summary>En el orden del libro (por avance), y si coinciden, por fecha.</summary>
    public static IEnumerable<Annotation> Ordered(IEnumerable<Annotation> annotations) =>
        annotations.OrderBy(a => a.Progress).ThenBy(a => a.CreatedAt);

    private static string Percent(double progress) => $"{Math.Round(progress * 100)} %";

    private static string Yaml(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // Comillas si hace falta: el texto puede traer ; " o saltos de línea.
    private static string Cell(string text) =>
        text.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
}
