using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Readers;

// Prueba: ¿qué archivo roto deja colgada la lectura?
string folder = args[0];
foreach (string file in Directory.GetFiles(folder))
{
    var sw = Stopwatch.StartNew();
    var task = Task.Run(() =>
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(file, new ReaderOptions());
            return $"abre, {archive.Entries.Count()} entradas";
        }
        catch (Exception e) { return $"error {e.GetType().Name}: {e.Message}"; }
    });
    bool done = task.Wait(TimeSpan.FromSeconds(5));
    Console.WriteLine($"{Path.GetFileName(file)}: {(done ? task.Result : "¡SE CUELGA (más de 5 s)!")} [{sw.ElapsedMilliseconds} ms]");
}
