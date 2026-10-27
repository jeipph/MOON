namespace Moon.Core.Library;

/// <summary>Una unidad conectada: su raíz ("E:\") y su nombre ("Libros").</summary>
public sealed record DriveSnapshot(string Root, string? Label);

/// <summary>
/// Encuentra una carpeta enlazada aunque Windows haya cambiado la letra del disco
/// (hoy es E:, mañana F:). Se reconoce por el nombre del disco y la misma ruta dentro de él.
/// </summary>
public static class SourceLocator
{
    /// <param name="exists">Comprueba si una carpeta existe (en las pruebas se simula).</param>
    /// <returns>La ruta actual de la carpeta, o null si su disco no está conectado.</returns>
    public static string? Locate(LibrarySource source, IEnumerable<DriveSnapshot> drives, Func<string, bool> exists)
    {
        string root = Path.GetPathRoot(source.Path) ?? "";
        var drivesList = drives.ToList();
        DriveSnapshot? sameRoot = drivesList.FirstOrDefault(d => string.Equals(d.Root, root, StringComparison.OrdinalIgnoreCase));

        // Lo normal: la carpeta sigue ahí. Solo se desconfía si en esa letra hay otro disco con otro nombre.
        bool otherDisk = sameRoot is not null && source.VolumeLabel is not null && !string.Equals(sameRoot.Label, source.VolumeLabel, StringComparison.OrdinalIgnoreCase);
        if (!otherDisk && exists(source.Path))
            return source.Path;

        if (string.IsNullOrEmpty(source.VolumeLabel) || root.Length == 0)
            return null;

        string inside = source.Path[root.Length..];
        foreach (DriveSnapshot drive in drivesList)
        {
            if (!string.Equals(drive.Label, source.VolumeLabel, StringComparison.OrdinalIgnoreCase))
                continue;
            string candidate = Path.Combine(drive.Root, inside);
            if (exists(candidate))
                return candidate;
        }
        return null;
    }
}
