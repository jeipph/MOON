using Microsoft.Win32;

namespace Moon.Windows.Shell;

public static class SystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>True si Windows tiene las apps en modo claro (Configuración → Personalización → Colores).</summary>
    public static bool AppsUseLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception)
        {
            return false; // Si no se puede leer, oscuro (el estilo base de la app).
        }
    }
}
