namespace SalemBonus.Application.Pos.Analytics;

/// <summary>
/// Жалпы сүзгілер (ТЗ «Статистика» §5, §24): кезең — дүкеннің жергілікті күндері (екі шеті қоса), дүкендер — «id,id»
/// (бос — ағымдағы дүкен), салыстыру — none / prev / year. Қалғаны — вкладканың жергілікті сүзгілері.
/// </summary>
public class AnalyticsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? StoreIds { get; set; }
    public string? Compare { get; set; }

    public string? Search { get; set; }
    public Guid? NodeId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? RegisterId { get; set; }
    public Guid? StaffUserId { get; set; }
    public string? Method { get; set; }
    /// <summary>Клиенттер: buyers / new / repeat / regular / inactive. Склад: in / out / belowMin.</summary>
    public string? Segment { get; set; }
    /// <summary>«Давно не покупали»: 30 / 60 / 90.</summary>
    public int? InactiveDays { get; set; }
    public bool? HasReturns { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record AnalyticsStoreDto(Guid Id, string Name);

/// <summary>
/// Есептің контексі: кезең, гранулярлық, салыстыру аралығы, құқықтар (фронт батырманы жасырады, сервер бәрібір тексереді),
/// «бүгін аяқталмаған» белгісі және жаңартылған уақыт (§9, §26).
/// </summary>
public record AnalyticsMetaDto(
    IReadOnlyList<AnalyticsStoreDto> Stores, DateOnly From, DateOnly To, string TimeZone, string Granularity,
    string Compare, DateOnly? CompareFrom, DateOnly? CompareTo, bool IncludesToday, DateTime GeneratedAt,
    bool CanFinance, bool CanContacts, bool CanExport);

/// <summary>
/// KPI: мәні, салыстыру мәні, абсолют және пайыз айырмасы (база 0 болса — null, «Нет базы для сравнения»).
/// Amount — екінші мән (қайтарымда сома), Numerator/Denominator — үлесті түсіндіру үшін (§8).
/// Available=false — дерек жоқ не құқық жоқ («Недоступно», жалған 0 емес).
/// </summary>
public record KpiDto(
    string Key, decimal? Value, decimal? Previous, decimal? ChangeAbs, decimal? ChangePercent,
    decimal? Amount = null, decimal? PreviousAmount = null, decimal? Numerator = null, decimal? Denominator = null,
    bool Available = true, string? Note = null);

/// <summary>Уақыт аралығы (bucket): күн, апта не ай — Start/End қоса, бір күн бір рет қана (§9–10).</summary>
public record BucketDto(DateOnly Start, DateOnly End);

/// <summary>Kind: money / count / qty / points. Style: bar / line. Values — бакеттер ретімен, ұзындығы бірдей.</summary>
public record SeriesDto(string Key, string Kind, string Style, IReadOnlyList<decimal> Values);

public record ShareDto(string Key, string? Label, decimal Value, decimal Percent, int? Count = null);

public record BreakdownDto(string Key, string Kind, IReadOnlyList<ShareDto> Items, decimal Total);

/// <summary>Kind: text / money / qty / int / percent / date / datetime / points. Link — «/products/{productId}» үлгісі.</summary>
public record ColumnDto(string Key, string Kind, bool Sortable = false, string? Link = null);

public record TableDto(IReadOnlyList<ColumnDto> Columns, IReadOnlyList<Dictionary<string, object?>> Rows, int Total, int Page, int PageSize,
    string? Sort = null, string? Dir = null);

/// <summary>Вкладканың бір пішіні: KPI, бакеттер мен сериялар, бөлінулер, кесте, ескертпелер (формула мен шектеулер).</summary>
public record AnalyticsTabDto(
    AnalyticsMetaDto Meta,
    IReadOnlyList<KpiDto> Kpis,
    IReadOnlyList<BucketDto> Buckets,
    IReadOnlyList<SeriesDto> Series,
    IReadOnlyList<BreakdownDto> Breakdowns,
    TableDto? Table,
    IReadOnlyList<string> Notes);

// ---------- «Динамика продаж» (§9, §27) ----------

public record TrendPointDto(DateOnly BucketStart, DateOnly BucketEnd, decimal Revenue, int SalesCount, decimal Returns, decimal NetSales);

public record SalesTrendDto(string Granularity, string TimeZone, DateOnly StartDate, DateOnly EndDate, bool IncludesToday, DateTime GeneratedAt,
    IReadOnlyList<TrendPointDto> Data);

// ---------- Обзор (§8, §11–14) ----------

public record PaymentShareDto(string Method, decimal Amount, decimal Percent, int Receipts);

public record PaymentMethodsDto(IReadOnlyList<PaymentShareDto> Items, decimal Total, int MixedReceipts);

public record TopProductDto(Guid ProductId, string Name, string? Article, string? ImageUrl, string? Unit, decimal Quantity, decimal Amount,
    decimal? Cost, decimal? Profit);

public record TopProductsDto(IReadOnlyList<TopProductDto> ByQuantity, IReadOnlyList<TopProductDto> ByAmount, IReadOnlyList<TopProductDto>? ByProfit,
    bool ProfitAvailable);

public record FrequencyDto(string Key, int Customers, decimal Percent);

public record ActivityCustomerDto(Guid Id, string Name, string? Phone, int Purchases, decimal Amount, DateTime LastAt);

public record CustomersBlockDto(
    int Buyers, int New, int Repeat, decimal? ReturningRate, int UnidentifiedReceipts, int? NewPrevious, int? RepeatPrevious,
    IReadOnlyList<FrequencyDto> Frequency, int RegularThreshold,
    IReadOnlyList<ActivityCustomerDto> NewList, IReadOnlyList<ActivityCustomerDto> RegularList, IReadOnlyList<ActivityCustomerDto> InactiveList, int InactiveDays);

public record InventoryBlockDto(int WithStock, int WithoutStock, int BelowMin, int Movements, decimal TotalUnits, decimal? Value, bool ValueComplete);

public record SuppliersBlockDto(decimal Purchases, int ReceiptDocuments, decimal Paid, decimal Debt, int UnpaidDocuments);

public record FinanceBlockDto(decimal Inflow, decimal Outflow, decimal NetFlow, decimal CustomerDebt);

public record OverviewDto(
    AnalyticsMetaDto Meta,
    IReadOnlyList<KpiDto> Kpis,
    PaymentMethodsDto PaymentMethods,
    TopProductsDto TopProducts,
    CustomersBlockDto Customers,
    InventoryBlockDto Inventory,
    SuppliersBlockDto Suppliers,
    FinanceBlockDto? Finance);
