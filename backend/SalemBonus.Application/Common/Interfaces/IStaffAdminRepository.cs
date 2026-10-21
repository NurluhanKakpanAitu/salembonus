using SalemBonus.Domain.Core;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Әрекет журналын сүзу (бизнес ішінде): қызметкер, дүкен, әрекет кілтінің басы, кезең, бет.</summary>
public record AuditQuery(Guid? StaffUserId, Guid? StoreId, string? Action, DateTime? From, DateTime? To, int Skip, int Take);

/// <summary>Қызметкердің сатылым көрсеткіші (соңғы 30 күн) — «Сотрудники» тізімі үшін.</summary>
public record StaffSalesStats(int Sales, decimal Revenue, DateTime? LastSaleAt);

/// <summary>«Сотрудники» мен «Настройка» бөлімдері: бизнестің қызметкерлері, сеанстары, әрекет журналы.</summary>
public interface IStaffAdminRepository
{
    /// <summary>Бизнестің барлық қызметкері (өшірілгендерімен), барлық мүшелігімен (өшірілгендерімен) және дүкендерімен. Оқу үшін.</summary>
    Task<IReadOnlyList<StaffUser>> ListAsync(Guid orgId, CancellationToken ct = default);
    /// <summary>Өзгерту үшін (tracked): барлық мүшелігімен (өшірілгендерімен) және дүкендерімен.</summary>
    Task<StaffUser?> GetForUpdateAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
    void Add(StaffUser user);
    void AddMembership(StoreMembership membership);
    /// <summary>Басқа белсенді иелер саны (белсенді дүкенде белсенді Owner мүшелігі бар, өзі белсенді).</summary>
    Task<int> OtherActiveOwnersAsync(Guid orgId, Guid exceptStaffId, CancellationToken ct = default);

    /// <summary>Қызметкердің ашық сеанстары (жабылмаған, мерзімі өтпеген), жаңасы алда.</summary>
    Task<IReadOnlyList<StaffRefreshToken>> SessionsAsync(Guid staffUserId, CancellationToken ct = default);
    Task<bool> RevokeSessionAsync(Guid staffUserId, Guid tokenId, CancellationToken ct = default);
    /// <summary>Барлық сеансты жабу; exceptHash — ағымдағы сеанс (ол қалады).</summary>
    Task RevokeSessionsAsync(Guid staffUserId, string? exceptHash, CancellationToken ct = default);

    /// <summary>Бизнестің әрекет журналы: бизнес, оның дүкендері мен қызметкерлері бойынша жазбалар.</summary>
    Task<(IReadOnlyList<AuditEntry> Rows, int Total)> AuditAsync(Guid orgId, AuditQuery q, CancellationToken ct = default);
    /// <summary>Журналдағы әрекет кілттері (сүзгі тізімі үшін).</summary>
    Task<IReadOnlyList<string>> AuditActionsAsync(Guid orgId, CancellationToken ct = default);
    Task<Dictionary<Guid, StaffSalesStats>> SalesStatsAsync(Guid orgId, DateTime sinceUtc, CancellationToken ct = default);
    Task<Dictionary<Guid, string>> RegisterNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
