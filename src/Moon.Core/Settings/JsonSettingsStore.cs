using System.Text.Json;
using System.Text.Json.Serialization;
using Moon.Core.Abstractions;
using Moon.Core.Models;

namespace Moon.Core.Settings;

/// <summary>
/// Guarda las opciones en JSON legible. Escritura atómica (.tmp y renombrar).
/// Si el archivo está dañado se aparta como <c>settings.json.bad</c> y se usan los valores por defecto.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // "Dark" en vez de 1: el archivo se entiende al abrirlo.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly Lock _lock = new();

    public JsonSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MOON", "settings.json");
    }

    public AppSettings Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
                return new AppSettings();

            try
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                // Se guarda aparte para poder mirarlo; la app sigue con los valores por defecto.
                File.Copy(_path, _path + ".bad", overwrite: true);
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, _path, overwrite: true);
        }
    }
}
