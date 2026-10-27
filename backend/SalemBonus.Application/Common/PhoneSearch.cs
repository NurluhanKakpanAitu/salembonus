namespace SalemBonus.Application.Common;

/// <summary>
/// Телефон бойынша іздеу — барлық тізім мен кассада бірдей ереже. Базада нөмір «+77071700774» түрінде, ал адам оны кез келген
/// түрде тереді: соңы «0774», ортасы «707 170», толық «+7 (707) 170-07-74» не «8 707 170 07 74» («8» — ел коды) — бәрі табылады.
/// Телефон ретінде тек сан мен «+ ( ) - .» белгілерінен тұратын сұраныс алынады: әріп аралас мәтіндегі сандар (аты, артикул, QR)
/// телефонмен салыстырылмайды.
/// </summary>
/// <param name="Digits">Нөмірдің кез келген жерінде кездесетін сандар.</param>
/// <param name="Prefix">«8…» деп терілгенде — «+7…» деп басталатын нөмір (8 ортадағы сан болуы да мүмкін, сондықтан Digits те тексеріледі).</param>
public sealed record PhoneSearch(string Digits, string? Prefix)
{
    public const int MinDigits = 3;

    public static PhoneSearch? Parse(string? term, int minDigits = MinDigits)
    {
        if (string.IsNullOrWhiteSpace(term)) return null;
        var t = term.Trim();
        if (!t.All(ch => char.IsDigit(ch) || " +()-.".Contains(ch))) return null;
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length < minDigits) return null;
        var prefix = digits[0] == '8' && !t.StartsWith('+') ? "+7" + digits[1..] : null;
        return new PhoneSearch(digits, prefix);
    }

    /// <summary>Жадтағы тізім үшін: сақталған нөмір қалай жазылса да (бос орын, жақша) — тек сандары салыстырылады.</summary>
    public bool Matches(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return false;
        var normalized = "+" + new string(phone.Where(char.IsDigit).ToArray());
        return normalized.Contains(Digits) || (Prefix is not null && normalized.StartsWith(Prefix));
    }
}
