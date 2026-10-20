using System.Text;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Application.Pos.Inventory;

/// <summary>Сәйкестендіруге қатысатын каталог тауары: тек керек өрістер, тесттерде қолмен жасалады.</summary>
public record CatalogCandidate(Guid Id, string Name, string? Article, IReadOnlyList<string> Barcodes);

public enum MatchKind { None, Barcode, Article, Name }

/// <summary>Matched — сенімді; Review — ұқсас, қолмен растау керек; NotFound — табылмады.</summary>
public enum MatchStatus { Matched, Review, NotFound }

public record LineMatch(RecognizedLine Line, Guid? ProductId, MatchKind Kind, MatchStatus Status, decimal Confidence);

/// <summary>
/// Танылған жолдарды каталогпен сәйкестендіру (ТЗ «Склад» §8): алдымен нақты штрихкод, сосын артикул, сосын атау.
/// Атау бойынша — нормаланған мәтіннің ұқсастығы (сөздер + биграммалар): ≥ <see cref="NameMatched"/> — сенімді,
/// ≥ <see cref="NameReview"/> — «требует выбора», одан төмен — табылмады. Таза функция, желісіз — бірлік тесттерде тексеріледі.
/// </summary>
public static class InvoiceLineMatcher
{
    public const decimal NameMatched = 0.85m;
    public const decimal NameReview = 0.45m;

    public static IReadOnlyList<LineMatch> Match(IReadOnlyList<RecognizedLine> lines, IReadOnlyList<CatalogCandidate> catalog)
    {
        var byBarcode = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var byArticle = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var articleCollisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in catalog)
        {
            foreach (var b in p.Barcodes)
            {
                var code = NormalizeCode(b);
                if (code.Length > 0) byBarcode.TryAdd(code, p.Id);
            }
            var article = NormalizeCode(p.Article);
            if (article.Length == 0) continue;
            // Екі тауарда бір артикул болса — ол бойынша сенімді сәйкестік жоқ.
            if (!byArticle.TryAdd(article, p.Id)) articleCollisions.Add(article);
        }
        var names = catalog.Select(p => (p.Id, Tokens: Tokenize(p.Name), Bigrams: Bigrams(Normalize(p.Name)))).ToList();

        return lines.Select(line =>
        {
            var barcode = NormalizeCode(line.Barcode);
            if (barcode.Length > 0 && byBarcode.TryGetValue(barcode, out var pid))
                return new LineMatch(line, pid, MatchKind.Barcode, MatchStatus.Matched, 1m);
            var article = NormalizeCode(line.Article);
            if (article.Length > 0 && !articleCollisions.Contains(article) && byArticle.TryGetValue(article, out pid))
                return new LineMatch(line, pid, MatchKind.Article, MatchStatus.Matched, 1m);
            return MatchByName(line, names);
        }).ToList();
    }

    private static LineMatch MatchByName(RecognizedLine line, List<(Guid Id, HashSet<string> Tokens, HashSet<string> Bigrams)> names)
    {
        var normalized = Normalize(line.Name);
        if (normalized.Length < 2) return new LineMatch(line, null, MatchKind.None, MatchStatus.NotFound, 0);
        var tokens = Tokenize(line.Name);
        var bigrams = Bigrams(normalized);
        Guid? best = null;
        var bestScore = 0m;
        var runnerUp = 0m;
        foreach (var (id, candTokens, candBigrams) in names)
        {
            var score = Similarity(tokens, bigrams, candTokens, candBigrams);
            if (score > bestScore) { runnerUp = bestScore; bestScore = score; best = id; }
            else if (score > runnerUp) runnerUp = score;
        }
        if (best is null || bestScore < NameReview) return new LineMatch(line, null, MatchKind.None, MatchStatus.NotFound, Math.Round(bestScore, 2));
        // Екі тауар бірдей дәрежеде ұқсас болса — адам таңдасын.
        var status = bestScore >= NameMatched && bestScore - runnerUp >= 0.1m ? MatchStatus.Matched : MatchStatus.Review;
        return new LineMatch(line, best, MatchKind.Name, status, Math.Round(bestScore, 2));
    }

    /// <summary>Сөздер бойынша Жаккар мен биграммалар бойынша Дайс коэффициентінің орташасы.</summary>
    public static decimal Similarity(string a, string b) =>
        Similarity(Tokenize(a), Bigrams(Normalize(a)), Tokenize(b), Bigrams(Normalize(b)));

    private static decimal Similarity(HashSet<string> tokensA, HashSet<string> bigramsA, HashSet<string> tokensB, HashSet<string> bigramsB)
    {
        if (tokensA.Count == 0 || tokensB.Count == 0) return 0;
        var tokenUnion = tokensA.Count + tokensB.Count - tokensA.Intersect(tokensB).Count();
        var jaccard = tokenUnion == 0 ? 0m : (decimal)tokensA.Intersect(tokensB).Count() / tokenUnion;
        var dice = bigramsA.Count + bigramsB.Count == 0 ? 0m : 2m * bigramsA.Intersect(bigramsB).Count() / (bigramsA.Count + bigramsB.Count);
        return (jaccard + dice) / 2;
    }

    /// <summary>
    /// Кіші әріп, «ё» → «е», тыныс белгілерінің орнына бос орын, сан мен әріптің шекарасында да бос орын
    /// («2,5% 1л» = «2.5 % 1 л»), бір бос орын.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        var space = true;
        var lastWasDigit = false;
        foreach (var raw in text.ToLowerInvariant().Normalize(NormalizationForm.FormKC))
        {
            var ch = raw == 'ё' ? 'е' : raw;
            if (!char.IsLetterOrDigit(ch)) { if (!space) { sb.Append(' '); space = true; } continue; }
            var digit = char.IsDigit(ch);
            if (!space && digit != lastWasDigit) sb.Append(' ');
            sb.Append(ch);
            space = false;
            lastWasDigit = digit;
        }
        return sb.ToString().Trim();
    }

    /// <summary>Штрихкод пен артикул: бос орын мен сызықшасыз салыстырылады («ART-001» = «art 001»).</summary>
    public static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? string.Empty : new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static HashSet<string> Tokenize(string? text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Stem).ToHashSet(StringComparer.Ordinal);

    /// <summary>Орыс/қазақ жалғауларының қарапайым қысқартуы: «молоко» ~ «молока», ұзын сөздің алғашқы 5 әрпі.</summary>
    private static string Stem(string token) => token.Length > 5 && token.All(char.IsLetter) ? token[..5] : token;

    private static HashSet<string> Bigrams(string normalized)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var compact = normalized.Replace(" ", string.Empty);
        for (var i = 0; i + 1 < compact.Length; i++) set.Add(compact.Substring(i, 2));
        return set;
    }
}
