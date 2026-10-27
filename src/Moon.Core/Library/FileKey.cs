using System.Security.Cryptography;

namespace Moon.Core.Library;

/// <summary>
/// "Huella" de un archivo para reconocerlo aunque se mueva o se renombre.
/// Usa el tamaño y el principio y el final del archivo (no todo: en un disco externo,
/// leer libros enteros de cientos de MB sería lentísimo).
/// </summary>
public static class FileKey
{
    private const int ChunkSize = 64 * 1024;

    public static string Compute(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 1, FileOptions.SequentialScan);
        long length = stream.Length;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(BitConverter.GetBytes(length));

        byte[] buffer = new byte[ChunkSize];
        hash.AppendData(buffer, 0, ReadUpTo(stream, buffer));
        if (length > ChunkSize)
        {
            stream.Seek(Math.Max(ChunkSize, length - ChunkSize), SeekOrigin.Begin);
            hash.AppendData(buffer, 0, ReadUpTo(stream, buffer));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        int total = 0;
        int read;
        while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            total += read;
        return total;
    }
}
