using System.Windows;
using System.Windows.Media;

namespace Moon.App.Windows;

internal static class VisualTreeExtensions
{
    /// <summary>El primer contenedor de tipo <typeparamref name="T"/> que envuelve a este elemento.</summary>
    public static T? FindAncestor<T>(this DependencyObject? element) where T : DependencyObject
    {
        while (element is not null and not T)
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        return element as T;
    }

    /// <summary>El primer elemento de tipo <typeparamref name="T"/> dentro de este (p. ej. el ScrollViewer de una lista).</summary>
    public static T? FindDescendant<T>(this DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, i);
            if (child is T found)
                return found;
            if (child.FindDescendant<T>() is { } deeper)
                return deeper;
        }
        return null;
    }
}
