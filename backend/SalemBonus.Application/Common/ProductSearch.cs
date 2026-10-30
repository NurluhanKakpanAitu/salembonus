namespace SalemBonus.Application.Common;

/// <summary>
/// Тауарды іздеу — касса, «Товары», «Склад», статистика бәрінде бірдей ереже. Сұраныс сөздерге бөлінеді, әр сөз тауардың
/// кез келген өрісінде (атауы, артикулы, штрихкоды, бренді, санаты, сипаттамалары, жеткізушісі, өндірушісі, елі, сипаттамасы)
/// кездесуі керек; сөздердің реті маңызды емес: «масло 5W-40» мен «5W-40 масло» — бірдей нәтиже.
/// </summary>
public static class ProductSearch
{
    /// <summary>Тым ұзын сұраныс базаны ауырлатпасын — алғашқы 8 сөз.</summary>
    public const int MaxTokens = 8;

    public static IReadOnlyList<string> Tokens(string? text) =>
        string.IsNullOrWhiteSpace(text) ? []
            : text.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTokens).ToList();

    /// <summary>Жадтағы тізім үшін: әр сөз берілген өрістердің біреуінде кездеседі.</summary>
    public static bool Matches(IReadOnlyList<string> tokens, params string?[] fields) =>
        tokens.All(t => fields.Any(f => f is not null && f.Contains(t, StringComparison.CurrentCultureIgnoreCase)));
}
