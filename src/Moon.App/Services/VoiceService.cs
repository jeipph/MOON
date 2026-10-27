using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.SpeechSynthesis;

namespace Moon.App.Services;

/// <summary>Una voz para leer: un audio de referencia (Chatterbox) o la voz de Windows.</summary>
public sealed record VoiceOption(string Name, string? ReferencePath)
{
    public const string WindowsName = "Voz de Windows";

    public bool IsWindows => ReferencePath is null;

    public override string ToString() => Name;
}

/// <summary>
/// Convierte texto en audio en este PC, sin internet:
/// - Chatterbox (voz natural clonada de un audio de la carpeta de voces), con un servicio en Python
///   que MOON arranca la primera vez que hace falta y cierra al salir;
/// - o la voz de Windows, si el motor de voz no está instalado.
/// </summary>
public sealed class VoiceService : IDisposable
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private Process? _server;
    private Uri? _baseUri;

    /// <summary>Python del motor de voz (instalado con <c>tools\instalar-voz.ps1</c>).</summary>
    public static string PythonPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOON", "voz", ".venv", "Scripts", "python.exe");

    public static string ServerScript => Path.Combine(AppContext.BaseDirectory, "Voice", "moon_voice.py");

    /// <summary>Carpeta de voces: cada audio de 10–15 s (.wav o .mp3) es una voz.</summary>
    public static string VoicesFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MOON", "voces");

    public static bool IsNaturalVoiceInstalled => File.Exists(PythonPath) && File.Exists(ServerScript);

    /// <summary>Voces disponibles: las de la carpeta (si el motor está instalado) y la de Windows.</summary>
    public static IReadOnlyList<VoiceOption> Voices()
    {
        var voices = new List<VoiceOption>();
        if (IsNaturalVoiceInstalled && Directory.Exists(VoicesFolder))
        {
            foreach (string file in Directory.EnumerateFiles(VoicesFolder)
                         .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".wav" or ".mp3" or ".flac")
                         .Order(StringComparer.CurrentCultureIgnoreCase))
                voices.Add(new VoiceOption(Path.GetFileNameWithoutExtension(file), file));
        }
        voices.Add(new VoiceOption(VoiceOption.WindowsName, null));
        return voices;
    }

    /// <summary>¿Ya está cargado el motor (la primera vez tarda ~30 s)?</summary>
    public bool IsWarm { get; private set; }

    /// <summary>Audio WAV de la frase, o null si falló (se salta la frase).</summary>
    public async Task<byte[]?> SpeakAsync(string text, string language, VoiceOption voice, CancellationToken cancel)
    {
        try
        {
            return voice.IsWindows
                ? await WindowsVoiceAsync(text, language)
                : await NaturalVoiceAsync(text, language, voice.ReferencePath!, cancel);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<byte[]?> NaturalVoiceAsync(string text, string language, string reference, CancellationToken cancel)
    {
        Uri baseUri = await EnsureServerAsync(cancel);
        using HttpResponseMessage response = await _http.PostAsJsonAsync(new Uri(baseUri, "speak"),
            new { text, language, voice = reference, exaggeration = 0.5, cfg_weight = 0.5 }, cancel);
        if (!response.IsSuccessStatusCode)
            return null;
        IsWarm = true;
        return await response.Content.ReadAsByteArrayAsync(cancel);
    }

    // Arranca el servicio de voz (oculto) la primera vez y espera a que el modelo esté cargado.
    private async Task<Uri> EnsureServerAsync(CancellationToken cancel)
    {
        await _startLock.WaitAsync(cancel);
        try
        {
            if (_server is { HasExited: false } && _baseUri is not null)
                return _baseUri;
            int port = FreePort();
            _server = Process.Start(new ProcessStartInfo(PythonPath)
            {
                ArgumentList = { ServerScript, port.ToString(), Environment.ProcessId.ToString() },
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(ServerScript)!,
            }) ?? throw new InvalidOperationException("No se pudo arrancar el motor de voz.");
            _baseUri = new Uri($"http://127.0.0.1:{port}/");
            // Cargar el modelo y "calentar" la tarjeta gráfica tarda: hasta 3 minutos la primera vez.
            for (int i = 0; i < 360; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (_server.HasExited)
                    throw new InvalidOperationException("El motor de voz se cerró al arrancar.");
                try
                {
                    using HttpResponseMessage health = await _http.GetAsync(new Uri(_baseUri, "health"), cancel);
                    if (health.StatusCode == HttpStatusCode.OK)
                        return _baseUri;
                }
                catch (HttpRequestException)
                {
                    // Aún no escucha: se vuelve a probar.
                }
                await Task.Delay(500, cancel);
            }
            throw new InvalidOperationException("El motor de voz tardó demasiado en cargar.");
        }
        finally
        {
            _startLock.Release();
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // Voz de Windows (más robótica, pero sin instalar nada): se elige una del idioma del libro.
    private static async Task<byte[]?> WindowsVoiceAsync(string text, string language)
    {
        using var synthesizer = new SpeechSynthesizer();
        VoiceInformation? voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));
        if (voice is not null)
            synthesizer.Voice = voice;
        using SpeechSynthesisStream stream = await synthesizer.SynthesizeTextToStreamAsync(text);
        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, global::Windows.Storage.Streams.InputStreamOptions.None);
        return bytes;
    }

    public void Dispose()
    {
        try
        {
            if (_server is { HasExited: false })
                _server.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Ya se había cerrado.
        }
        _server?.Dispose();
        _http.Dispose();
        _startLock.Dispose();
    }
}
