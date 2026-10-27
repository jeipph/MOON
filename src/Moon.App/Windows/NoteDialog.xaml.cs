using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Moon.Core.Annotations;

namespace Moon.App.Windows;

public enum NoteDialogResult
{
    Cancel,
    Save,
    Delete,
}

/// <summary>Escribir o cambiar la nota y el color de un subrayado (o de una zona de cómic).</summary>
public partial class NoteDialog : Window
{
    public NoteDialog(Window owner, string quote, string note, HighlightColor color, bool showColors = true)
    {
        InitializeComponent();
        Owner = owner;
        QuoteText.Text = quote;
        NoteBox.Text = note;
        Color = color;
        ColorLabel.Visibility = ColorRow.Visibility = showColors ? Visibility.Visible : Visibility.Collapsed;
        BuildColorRow();
        Loaded += (_, _) =>
        {
            NoteBox.Focus();
            NoteBox.CaretIndex = NoteBox.Text.Length;
        };
    }

    public NoteDialogResult Result { get; private set; } = NoteDialogResult.Cancel;

    public string Note => NoteBox.Text.Trim();

    public HighlightColor Color { get; private set; }

    /// <summary>Muestra la ventana y devuelve qué eligió la persona.</summary>
    public NoteDialogResult Ask()
    {
        ShowDialog();
        return Result;
    }

    // Un círculo por color; el elegido lleva borde.
    private void BuildColorRow()
    {
        ColorRow.Children.Clear();
        foreach (HighlightColor color in Enum.GetValues<HighlightColor>())
        {
            var dot = new Ellipse
            {
                Width = (double)FindResource("Size.ColorDot"),
                Height = (double)FindResource("Size.ColorDot"),
                Fill = (Brush)FindResource($"Brush.Highlight.{color}"),
                Stroke = color == Color ? (Brush)FindResource("Brush.Text") : Brushes.Transparent,
                StrokeThickness = 2,
            };
            var button = new Button
            {
                Content = dot,
                Style = (Style)FindResource("IconButton"),
                ToolTip = ColorName(color),
                Tag = color,
            };
            System.Windows.Automation.AutomationProperties.SetName(button, ColorName(color));
            button.Click += (_, _) =>
            {
                Color = (HighlightColor)button.Tag;
                BuildColorRow();
            };
            ColorRow.Children.Add(button);
        }
        QuoteBorder.BorderBrush = (Brush)FindResource($"Brush.Highlight.{Color}");
    }

    public static string ColorName(HighlightColor color) => color switch
    {
        HighlightColor.Yellow => "Amarillo",
        HighlightColor.Green => "Verde",
        HighlightColor.Blue => "Azul",
        HighlightColor.Pink => "Rosa",
        _ => "Morado",
    };

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        Result = NoteDialogResult.Save;
        Close();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        Result = NoteDialogResult.Delete;
        Close();
    }
}
