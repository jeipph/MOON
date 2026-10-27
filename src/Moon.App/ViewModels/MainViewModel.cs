using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Moon.App.Services;
using Moon.Core.Abstractions;
using Moon.Core.Models;

namespace Moon.App.ViewModels;

/// <summary>Ventana principal: la biblioteca y el tema.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly ThemeService _themes;
    private AppSettings _settings;

    public MainViewModel(ISettingsStore store, ThemeService themes, LibraryViewModel library)
    {
        _store = store;
        _themes = themes;
        Library = library;
        _settings = store.Load();
        _themes.Apply(_settings.Theme);
    }

    public LibraryViewModel Library { get; }

    public AppTheme Theme => _settings.Theme;

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
        _settings = _settings with { Theme = next };
        _themes.Apply(next);
        _store.Save(_settings);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(ThemeLabel));
    }
}
