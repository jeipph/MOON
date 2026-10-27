using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Models;

namespace Moon.App.ViewModels;

/// <summary>Ventana principal: la biblioteca y el tema.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;

    public MainViewModel(SettingsService settings, ThemeService themes, LibraryViewModel library)
    {
        _settings = settings;
        _themes = themes;
        Library = library;
        _themes.Apply(Theme);
    }

    public LibraryViewModel Library { get; }

    public AppTheme Theme => _settings.Current.Theme;

    /// <summary>Texto del botón de tema: muestra el tema actual.</summary>
    public string ThemeLabel => Theme switch
    {
        AppTheme.Dark => "Oscuro",
        AppTheme.Light => "Claro",
        _ => "Sistema",
    };

    /// <summary>Sistema → Oscuro → Claro → Sistema.</summary>
    [RelayCommand]
    private void NextTheme()
    {
        AppTheme next = Theme switch
        {
            AppTheme.System => AppTheme.Dark,
            AppTheme.Dark => AppTheme.Light,
            _ => AppTheme.System,
        };
        _settings.Update(s => s with { Theme = next });
        _themes.Apply(next);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(ThemeLabel));
    }
}
