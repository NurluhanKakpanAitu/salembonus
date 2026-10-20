using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Pos.Inventory;

namespace SalemBonus.Api.Tests;

/// <summary>Накладная жолдарын каталогпен сәйкестендіру (ТЗ «Склад» §8): штрихкод → артикул → атау. Таза функция, базасыз.</summary>
public class InvoiceLineMatcherTests
{
    private static readonly Guid Milk = Guid.NewGuid(), Bread = Guid.NewGuid(), Kefir = Guid.NewGuid(), Kefir2 = Guid.NewGuid();

    private static readonly List<CatalogCandidate> Catalog =
    [
        new(Milk, "Молоко Петропавловское 2.5% 1л", "MLK-025", ["4870000000011", "4870000000028"]),
        new(Bread, "Хлеб Бородинский", "BRD-01", []),
        new(Kefir, "Кефир 1% 0.5л", "KF-1", ["4870000000035"]),
        new(Kefir2, "Кефир 2.5% 0.5л", "KF-25", []),
    ];

    private static RecognizedLine Line(string? name = null, string? article = null, string? barcode = null) => new(name, article, barcode, 1, 100, 100);

    [Fact]
    public void Barcode_wins_over_article_and_name_even_when_the_recognized_name_is_garbage()
    {
        var m = Assert.Single(InvoiceLineMatcher.Match([Line("Сыр ???", "BRD-01", "4870000000028")], Catalog));

        Assert.Equal(Milk, m.ProductId);
        Assert.Equal(MatchKind.Barcode, m.Kind);
        Assert.Equal(MatchStatus.Matched, m.Status);
        Assert.Equal(1m, m.Confidence);
    }

    [Fact]
    public void Article_is_compared_without_case_spaces_and_dashes()
    {
        var m = Assert.Single(InvoiceLineMatcher.Match([Line("что-то", "brd 01")], Catalog));

        Assert.Equal(Bread, m.ProductId);
        Assert.Equal(MatchKind.Article, m.Kind);
        Assert.Equal(MatchStatus.Matched, m.Status);
    }

    [Fact]
    public void Article_shared_by_two_products_does_not_match_and_falls_back_to_name()
    {
        var catalog = Catalog.Concat([new CatalogCandidate(Guid.NewGuid(), "Другой хлеб", "BRD-01", [])]).ToList();

        var m = Assert.Single(InvoiceLineMatcher.Match([Line("Хлеб Бородинский", "BRD-01")], catalog));

        Assert.Equal(Bread, m.ProductId);
        Assert.Equal(MatchKind.Name, m.Kind);
    }

    [Fact]
    public void Name_match_tolerates_case_punctuation_and_word_order()
    {
        var m = Assert.Single(InvoiceLineMatcher.Match([Line("МОЛОКО 2,5% 1 л «Петропавловское»")], Catalog));

        Assert.Equal(Milk, m.ProductId);
        Assert.Equal(MatchKind.Name, m.Kind);
        Assert.Equal(MatchStatus.Matched, m.Status);
    }

    [Fact]
    public void Similar_products_need_a_human_choice_and_unknown_names_are_not_found()
    {
        var result = InvoiceLineMatcher.Match([Line("Кефир 0.5л"), Line("Шуруп саморез 4x40 оцинк."), Line("")], Catalog);

        Assert.Equal(MatchStatus.Review, result[0].Status); // екі кефир бірдей ұқсас — таңдау адамда
        Assert.Contains(result[0].ProductId!.Value, new[] { Kefir, Kefir2 });
        Assert.Equal(MatchStatus.NotFound, result[1].Status);
        Assert.Null(result[1].ProductId);
        Assert.Equal(MatchStatus.NotFound, result[2].Status);
        Assert.Equal(0m, result[2].Confidence);
    }

    [Fact]
    public void Unknown_barcode_does_not_block_a_name_match()
    {
        var m = Assert.Single(InvoiceLineMatcher.Match([Line("Хлеб бородинский", barcode: "9999999999999")], Catalog));

        Assert.Equal(Bread, m.ProductId);
        Assert.Equal(MatchKind.Name, m.Kind);
    }

    [Theory]
    [InlineData("Молоко 2.5% 1л", "молоко 2,5 % 1 л", 1.0)]
    [InlineData("Молоко", "Хлеб", 0.0)]
    public void Similarity_is_symmetric_and_bounded(string a, string b, double expected)
    {
        Assert.Equal((decimal)expected, Math.Round(InvoiceLineMatcher.Similarity(a, b), 2));
        Assert.Equal(InvoiceLineMatcher.Similarity(a, b), InvoiceLineMatcher.Similarity(b, a));
    }
}
