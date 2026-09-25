using System.Text.RegularExpressions;

namespace SalemBonus.Domain.Entities;

/// <summary>
/// КАТО атауларын тұтынушыға оқуға ыңғайлы етеді.
/// Жіктеуіште әкімшілік бірліктер де елді мекендермен қатар тұр: НК РК 11-2009 бойынша
/// аудандық/қалалық/кенттік әкімшілік пен ауылдық округ — бағыныстылықты білдіретін
/// шартты белгілеулер (3.13–3.16), адам тұратын жер емес.
/// </summary>
public static partial class KatoNames
{
    /// <summary>Әкімшілік бірлік пе (ауылдық округ, қалалық/кенттік/аудандық әкімшілік)?</summary>
    public static bool IsAdministrative(string name) => AdministrativeSuffix().IsMatch(name);

    /// <summary>Қала әкімдігін оқуға ыңғайлы етеді: "Көкшетау Қ.Ә." → "Көкшетау".</summary>
    public static string ForDisplay(string name) => AkimatSuffix().Replace(name, string.Empty).Trim();

    // а.о./с.о. — ауылдық (селолық) округ, қ.ә./г.а. — қалалық әкімшілік,
    // к.ә./п.а. — кенттік әкімшілік, а.ә./р.а. — аудандық әкімшілік.
    [GeneratedRegex(@"\s(а\.о\.|с\.о\.|қ\.ә\.|к\.ә\.|а\.ә\.|п\.а\.|р\.а\.|с\.а\.|г\.а\.)\s*$")]
    private static partial Regex AdministrativeSuffix();

    // Қ.Ә./Г.А. — қала әкімдігі: бұл нақты қала, тек жұрнағы артық.
    [GeneratedRegex(@"\s(Қ\.Ә\.|Г\.А\.)\s*$")]
    private static partial Regex AkimatSuffix();
}
