namespace Moon.Core.Dictionary;

/// <summary>
/// Formas base de una palabra inglesa: "went" → "go", "children" → "child", "running" → "run".
/// No es perfecto: devuelve candidatas y el diccionario se queda con las que existen.
/// </summary>
public static class EnglishLemmatizer
{
    // Verbos y nombres irregulares más comunes (forma → base).
    private static readonly Dictionary<string, string> Irregular = BuildIrregular();

    /// <summary>La palabra tal cual primero y luego sus posibles formas base, sin repetir.</summary>
    public static IReadOnlyList<string> Candidates(string word)
    {
        string w = Clean(word);
        var result = new List<string>();
        void Add(string candidate)
        {
            if (candidate.Length > 0 && !result.Contains(candidate))
                result.Add(candidate);
        }

        Add(w);
        if (w.Length == 0)
            return result;
        if (Irregular.TryGetValue(w, out string? irregular))
            Add(irregular);

        // Plurales y tercera persona: cats → cat, boxes → box, cities → city, leaves → leaf
        if (w.EndsWith("ies") && w.Length > 4) Add(w[..^3] + "y");
        if (w.EndsWith("ves") && w.Length > 4) { Add(w[..^3] + "f"); Add(w[..^3] + "fe"); }
        if (w.EndsWith("es") && w.Length > 3) Add(w[..^2]);
        if (w.EndsWith('s') && !w.EndsWith("ss") && w.Length > 2) Add(w[..^1]);

        // Pasado: walked → walk, stopped → stop, loved → love, tried → try
        if (w.EndsWith("ied") && w.Length > 4) Add(w[..^3] + "y");
        if (w.EndsWith("ed") && w.Length > 3)
        {
            string stem = w[..^2];
            Add(stem);
            Add(stem + "e");
            if (HasDoubledEnding(stem)) Add(stem[..^1]);
        }

        // Gerundio: running → run, making → make, walking → walk, dying → die
        if (w.EndsWith("ying") && w.Length > 5) Add(w[..^4] + "ie");
        if (w.EndsWith("ing") && w.Length > 4)
        {
            string stem = w[..^3];
            Add(stem);
            Add(stem + "e");
            if (HasDoubledEnding(stem)) Add(stem[..^1]);
        }

        // Comparativos: bigger → big, happier → happy, nicest → nice
        foreach (string suffix in new[] { "er", "est" })
        {
            if (w.EndsWith("i" + suffix) && w.Length > suffix.Length + 2) Add(w[..^(suffix.Length + 1)] + "y");
            if (w.EndsWith(suffix) && w.Length > suffix.Length + 2)
            {
                string stem = w[..^suffix.Length];
                Add(stem);
                Add(stem + "e");
                if (HasDoubledEnding(stem)) Add(stem[..^1]);
            }
        }

        // Adverbios: quickly → quick, happily → happy
        if (w.EndsWith("ily") && w.Length > 4) Add(w[..^3] + "y");
        if (w.EndsWith("ly") && w.Length > 4) Add(w[..^2]);

        // Posesivo: Alice's → alice
        if (w.EndsWith("'s")) Add(w[..^2]);
        return result;
    }

    /// <summary>Minúsculas, sin signos alrededor y con el apóstrofo tipográfico normalizado.</summary>
    public static string Clean(string word) =>
        word.Trim().Replace('’', '\'').Trim('"', '“', '”', '\'', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '—', '-', '_', '*')
            .ToLowerInvariant();

    // "stopp" (de stopped) → termina en consonante doble.
    private static bool HasDoubledEnding(string stem) =>
        stem.Length > 2 && stem[^1] == stem[^2] && !"aeiouls".Contains(stem[^1]);

    private static Dictionary<string, string> BuildIrregular()
    {
        const string list =
            "was:be were:be been:be am:be is:be are:be had:have has:have did:do does:do done:do " +
            "went:go gone:go goes:go said:say made:make took:take taken:take came:come saw:see seen:see " +
            "knew:know known:know got:get gotten:get gave:give given:give found:find thought:think told:tell " +
            "became:become left:leave felt:feel brought:bring began:begin begun:begin kept:keep held:hold " +
            "wrote:write written:write stood:stand heard:hear meant:mean met:meet ran:run paid:pay sat:sit " +
            "spoke:speak spoken:speak lay:lie led:lead grew:grow grown:grow lost:lose fell:fall fallen:fall " +
            "sent:send built:build understood:understand drew:draw drawn:draw broke:break broken:break " +
            "spent:spend rose:rise risen:rise drove:drive driven:drive bought:buy wore:wear worn:wear " +
            "chose:choose chosen:choose ate:eat eaten:eat sang:sing sung:sing swam:swim swum:swim " +
            "threw:throw thrown:throw flew:fly flown:fly forgot:forget forgotten:forget hid:hide hidden:hide " +
            "caught:catch taught:teach fought:fight sought:seek slept:sleep sold:sell shook:shake shaken:shake " +
            "stole:steal stolen:steal struck:strike woke:wake woken:wake wept:weep won:win wound:wind " +
            "bit:bite bitten:bite blew:blow blown:blow dug:dig fed:feed fled:flee froze:freeze frozen:freeze " +
            "hung:hang knelt:kneel lit:light rode:ride ridden:ride rang:ring rung:ring shot:shoot shone:shine " +
            "sank:sink slid:slide smelt:smell spun:spin sprang:spring stuck:stick stung:sting swore:swear " +
            "tore:tear torn:tear trod:tread bore:bear born:bear bent:bend bound:bind clung:cling crept:creep " +
            "dealt:deal dreamt:dream leant:lean leapt:leap lent:lend meant:mean overcame:overcome " +
            "children:child men:man women:woman mice:mouse feet:foot teeth:tooth geese:goose people:person " +
            "better:good best:good worse:bad worst:bad further:far farther:far";
        var map = new Dictionary<string, string>();
        foreach (string pair in list.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split(':');
            map.TryAdd(parts[0], parts[1]);
        }
        return map;
    }
}
