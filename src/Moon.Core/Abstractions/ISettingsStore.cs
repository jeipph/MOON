using Moon.Core.Models;

namespace Moon.Core.Abstractions;

public interface ISettingsStore
{
    /// <summary>Lee las opciones; si no hay archivo o está dañado, devuelve los valores por defecto.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
