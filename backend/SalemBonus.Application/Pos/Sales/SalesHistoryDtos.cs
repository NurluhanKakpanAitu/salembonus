namespace SalemBonus.Application.Pos.Sales;

/// <summary>Сұраныс параметрлері (ТЗ «Продажи» §6): кезең — дүкеннің жергілікті күндері, екі шеті де қоса.</summary>
public class SalesHistoryQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Search { get; set; }
    public Guid? RegisterId { get; set; }
    public Guid? StaffUserId { get; set; }
    /// <summary>Cash / Card / Qr / Transfer / Debt — төлем құрамында бар ма (аралас чек бірнешеуіне сай келеді).</summary>
    public string? Method { get; set; }
    /// <summary>Completed / PartiallyReturned / Returned / Debt.</summary>
    public string? Status { get; set; }
    /// <summary>yes / no.</summary>
    public string? HasReturn { get; set; }
    public Guid? CustomerId { get; set; }
    /// <summary>Тек клиентсіз чектер.</summary>
    public bool NoCustomer { get; set; }
    /// <summary>number / date / total / returned / net / status.</summary>
    public string? Sort { get; set; }
    /// <summary>asc / desc.</summary>
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Кесте жолы (ТЗ §7). <see cref="Total"/> — чектің тарихи сомасы, қайтарымнан кейін өзгермейді;
/// <see cref="Net"/> = Total − Returned. Status: Completed / PartiallyReturned / Returned, ал ашық қарыз — Debt.
/// </summary>
public record SalesHistoryItemDto(
    Guid Id, long Number, DateTime CreatedAt, Guid? CustomerId, string? CustomerName, string CashierName, string RegisterName,
    int ItemCount, decimal Units, decimal Total, decimal Returned, decimal Net, string Status, IReadOnlyList<string> Methods,
    bool HasOpenDebt, decimal DebtRemaining);

public record SalesHistoryPageDto(IReadOnlyList<SalesHistoryItemDto> Items, int Total, int Page, int PageSize);

/// <summary>
/// KPI (ТЗ §5, §11). Сатылымдар — сату күнімен, қайтарымдар — қайтару күнімен. SalemPos-та төленбеген чек
/// болмайды, сондықтан «Отменён» мәртебесі жоқ: түзету — қайтарым. Оның орнына «В долг» (қарызға сатылғандар).
/// </summary>
public record SalesSummaryDto(
    int SalesCount, decimal SalesTotal, decimal? SalesChangePercent,
    int ReturnsCount, decimal ReturnsTotal, decimal ReturnsShare,
    int DebtCount, decimal DebtTotal, decimal DebtOpen,
    decimal AverageCheck,
    decimal Units,
    decimal Discounts, int DiscountCount, decimal DiscountsShare,
    int BonusRedeemed, int BonusCount, decimal BonusShare,
    int WithReturn, decimal WithReturnShare);

public record SalesFilterOptionDto(Guid Id, string Name);

public record SalesFiltersDto(IReadOnlyList<SalesFilterOptionDto> Registers, IReadOnlyList<SalesFilterOptionDto> Cashiers);

/// <summary>
/// Чек тарихының оқиғасы (ТЗ §8.4). Type: sale, payment, debt, discount, bonus.accrued, bonus.redeemed,
/// debt.repaid, return, debt.reduced, bonus.restored, bonus.reversed. Мәтінді интерфейс тілмен құрастырады.
/// </summary>
public record SaleEventDto(DateTime At, string Type, string? StaffName, decimal? Amount, string? Method, string? Note, Guid? ReturnId);
