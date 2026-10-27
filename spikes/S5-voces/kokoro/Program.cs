using System.Diagnostics;
using KokoroSharp;
using KokoroSharp.Core;

// Prueba S5: genera muestras de Kokoro en WAV y mide tiempos.
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../muestras"));
var es = File.ReadAllText(Path.Combine(outDir, "../texto-es.txt"));
var en = File.ReadAllText(Path.Combine(outDir, "../texto-en.txt"));

var sw = Stopwatch.StartNew();
var tts = KokoroWavSynthesizer.LoadModel();
Console.WriteLine($"Modelo cargado en {sw.Elapsed.TotalSeconds:F1} s");

foreach (var (voice, text) in new[] { ("ef_dora", es), ("em_alex", es), ("af_heart", en), ("bf_emma", en) })
{
    sw.Restart();
    var bytes = tts.Synthesize(text, KokoroVoiceManager.GetVoice(voice));
    var secs = sw.Elapsed.TotalSeconds;
    var file = Path.Combine(outDir, $"kokoro-{voice}.wav");
    KokoroWavSynthesizer.SaveAudioToFile(bytes, file);
    Console.WriteLine($"{voice}: generado en {secs:F1} s, {bytes.Length / 1024} KB");
}
Console.WriteLine($"RAM del proceso: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
