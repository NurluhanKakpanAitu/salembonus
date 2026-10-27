using System.Text.Json.Nodes;
using SalemBonus.Application.Common;
using static SalemBonus.Api.Tests.Pos;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Телефон бойынша іздеу барлық жерде бірдей: нөмірдің соңы, ортасы, «8 …» (ел коды) не «+7 (…) …-…» түрінде терілсе де табылады —
/// кассадағы клиент іздеу, «Клиенты», «Продажи» және «Бонусы» тізімдері. Әріп аралас мәтіндегі сандар телефонмен салыстырылмайды.
/// </summary>
[Collection(ApiCollection.Name)]
public class PhoneSearchTests(ApiFixture api)
{
    private static string Esc(string s) => Uri.EscapeDataString(s);

    /// <summary>«+7707ABCDEFG» → кассир теретін түрлер.</summary>
    private static string[] Forms(string phone)
    {
        var local = phone[2..]; // 707ABCDEFG
        return
        [
            local[^4..],                                                        // соңғы 4 сан
            $"{local[3..6]} {local[6..8]}",                                     // ортасы, бос орынмен
            $"8{local[..6]}",                                                   // «8707ABC» — басы, 8-бен
            $"8 {local[..3]} {local[3..6]} {local[6..8]} {local[8..]}",         // толық, 8-бен
            $"+7 ({local[..3]}) {local[3..6]}-{local[6..8]}-{local[8..]}",      // толық, жақшамен
        ];
    }

    private static async Task<(Pos Pos, Guid Id, string Phone)> CustomerWithSaleAsync(ApiFixture api)
    {
        var pos = await api.NewDeviceAsync();
        var phone = "+7707" + Random.Shared.Next(1_000_000, 9_999_999);
        var id = (await pos.Post(pos.Owner, "/api/pos/v1/cashier/customers", new { phone, firstName = "Нөмір", lastName = "Іздеу", birthDate = "1991-02-03" })).Ok["id"]!.GetValue<Guid>();
        var product = await pos.CreateProductAsync(price: 100, stock: 5);
        (await pos.SellAsync(pos.Owner, [Line(product, 1)], [Cash(100)], customerId: id)).EnsureOk();
        return (pos, id, phone);
    }

    [Fact]
    public async Task Cashier_finds_a_store_customer_by_any_part_or_form_of_the_phone()
    {
        var (pos, id, phone) = await CustomerWithSaleAsync(api);
        foreach (var form in Forms(phone))
        {
            var found = (await pos.Get(pos.Cashier, $"/api/pos/v1/cashier/customers?q={Esc(form)}")).Ok.AsArray();
            Assert.True(found.Any(c => c!["id"]!.GetValue<Guid>() == id), $"касса: «{form}» бойынша клиент табылмады");
        }
        // Әріп аралас мәтін — аты бойынша іздеу, нөмірдің сандары салыстырылмайды.
        Assert.DoesNotContain((await pos.Get(pos.Cashier, $"/api/pos/v1/cashier/customers?q={Esc("Жоқ" + phone[^4..])}")).Ok.AsArray(),
            c => c!["id"]!.GetValue<Guid>() == id);
    }

    [Fact]
    public async Task Clients_sales_and_bonus_lists_find_by_any_form_of_the_phone()
    {
        var (pos, id, phone) = await CustomerWithSaleAsync(api);
        foreach (var form in Forms(phone))
        {
            var clients = (await pos.Post(pos.Owner, "/api/pos/v1/clients/search", new { filter = new { search = form }, pageSize = 200 })).Ok["rows"]!.AsArray();
            Assert.True(clients.Any(r => r!["customerId"]!.GetValue<Guid>() == id), $"клиенты: «{form}»");

            var bonus = (await pos.Get(pos.Owner, $"/api/pos/v1/bonus/customers?search={Esc(form)}&pageSize=200")).Ok["items"]!.AsArray();
            Assert.True(bonus.Any(r => r!["customerId"]!.GetValue<Guid>() == id), $"бонусы: «{form}»");

            var sales = (await pos.Get(pos.Owner, $"/api/pos/v1/sales/history?search={Esc(form)}&pageSize=100")).Ok["items"]!.AsArray();
            Assert.True(sales.Any(s => s!["customerId"]?.GetValue<Guid>() == id), $"продажи: «{form}»");
        }
    }

    [Theory]
    [InlineData("0774", "+77071700774", true)]
    [InlineData("170 07", "+77071700774", true)]
    [InlineData("8707170", "+77071700774", true)]
    [InlineData("8 (707) 170-07-74", "+77071700774", true)]
    [InlineData("+7 707 170 07 74", "+7 707 170 07 74", true)] // админкадағы контакт нөмірі бос орынмен сақталған
    [InlineData("8707", "+77011238707", true)]                  // «8» — ортадағы сан да болуы мүмкін
    [InlineData("8701", "+77071700774", false)]
    [InlineData("12", "+77071700774", false)]                   // 3 саннан аз — телефон емес
    [InlineData("Айгерим 0774", "+77071700774", false)]         // әріп аралас — аты бойынша іздеу
    public void Phone_search_rules(string term, string phone, bool matches) =>
        Assert.Equal(matches, PhoneSearch.Parse(term)?.Matches(phone) ?? false);
}
