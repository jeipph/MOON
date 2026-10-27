namespace Moon.Core.Comics;

/// <summary>
/// Fuentes que saben el tamaño de cada página sin dibujarla (PDF). Sirve para colocar
/// las páginas del modo webtoon antes de cargarlas y para detectar páginas panorámicas.
/// </summary>
public interface IKnownPageSizes
{
    (double Width, double Height) PageSize(int index);
}
