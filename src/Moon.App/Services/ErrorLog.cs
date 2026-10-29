using System.IO;

namespace Moon.App.Services;

/// <summary>
/// Registro de errores en <c>%LOCALAPPDATA%\MOON\logs\error.log</c>: solo el error técnico, nunca
/// texto de los libros. Si pasa de 512 KB se aparta como <c>error.old.log</c>.
/// </summary>
public static class ErrorLog
{
    private const long MaxBytes = 512 * 1024;
    private static readonly Lock WriteLock = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MOON", "logs", "error.log");

    /// <summary>Nunca lanza: si no se puede escribir, se pierde en silencio.</summary>
    public static void Write(string message)
    {
        try
        {
            lock (WriteLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(FilePath, Path.ChangeExtension(FilePath, ".old.log"), overwrite: true);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n\n");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
