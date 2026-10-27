using System.Runtime.InteropServices;

namespace Moon.Windows.Shell;

/// <summary>Pone la barra de título de Windows en oscuro o claro, a juego con el tema de MOON.</summary>
public static partial class TitleBar
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 20H1 o posterior).
    private const int UseImmersiveDarkMode = 20;

    public static void SetDark(nint hwnd, bool dark)
    {
        int value = dark ? 1 : 0;
        // Si falla (Windows antiguo), la barra se queda como esté: no es grave.
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref value, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
