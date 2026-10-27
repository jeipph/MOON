using Moon.Core.Abstractions;
using Moon.Core.Models;

namespace Moon.App.Services;

/// <summary>
/// Las opciones de MOON, compartidas por todas las ventanas: cada cambio se guarda en disco
/// y se avisa a quien lo necesite (p. ej. la ventana de lectura cuando cambia el tema).
/// </summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store)
    {
        _store = store;
        Current = store.Load();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated = change(Current);
        if (updated == Current)
            return;
        Current = updated;
        _store.Save(updated);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
