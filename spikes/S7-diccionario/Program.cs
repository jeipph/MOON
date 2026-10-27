using System.Text;
using System.Xml.Linq;

// Convierte el diccionario bilingüe de Apertium (inglés-español, GPL-2.0) a un TSV compacto para MOON:
//   inglés \t categoría \t español
// Solo entradas que valen en la dirección inglés → español (sin r="RL").
string input = args[0], output = args[1];
XDocument dix = XDocument.Load(input);

static string Text(XElement? side)
{
    if (side is null) return "";
    var sb = new StringBuilder();
    foreach (XNode node in side.Nodes())
    {
        switch (node)
        {
            case XText t: sb.Append(t.Value); break;
            case XElement { Name.LocalName: "b" }: sb.Append(' '); break;
            case XElement { Name.LocalName: "g" } g: sb.Append(Text(g)); break;
        }
    }
    return sb.ToString().Trim();
}

static string Tags(XElement? side) =>
    string.Join('.', side?.Elements("s").Select(s => (string?)s.Attribute("n") ?? "") ?? []);

var pairs = new SortedSet<(string En, string Pos, string Es)>();
foreach (XElement e in dix.Descendants("e"))
{
    if ((string?)e.Attribute("r") == "RL")
        continue; // solo sirve español → inglés
    XElement? p = e.Element("p");
    XElement? l = p?.Element("l"), r = p?.Element("r");
    if (e.Element("i") is { } identity) { l = identity; r = identity; }
    string en = Text(l), es = Text(r);
    if (en.Length == 0 || es.Length == 0 || en.Contains('#') || es.Contains('#'))
        continue;
    string pos = Tags(l).Split('.')[0];
    string gender = Tags(r).Split('.') switch { var t when t.Contains("m") => " (m)", var t when t.Contains("f") => " (f)", _ => "" };
    pairs.Add((en.ToLowerInvariant(), pos, es + gender));
}

using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
writer.WriteLine("# Diccionario inglés → español para MOON. Origen: Apertium eng-spa (GPL-2.0), https://github.com/apertium/apertium-eng-spa");
foreach (var (en, pos, es) in pairs)
    writer.WriteLine($"{en}\t{pos}\t{es}");
Console.WriteLine($"{pairs.Count} pares, {pairs.Select(p => p.En).Distinct().Count()} palabras inglesas");
