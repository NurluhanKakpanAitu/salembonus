using SalemBonus.Domain.Core;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Platform;

namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Бизнестің қолданыс көрсеткіштері (әкімші тізімі мен карточкасы үшін).</summary>
public record OrgStats(int Stores, int Staff, int Sales30, decimal Revenue30, DateTime? LastSaleAt, int Clients, string? OwnerName, string? OwnerPhone,
    DateTime? LastLoginAt);

public record PlatformPaymentQuery(Guid? OrganizationId, DateOnly? From, DateOnly? To, string? Method, bool IncludeVoided, int Skip, int Take);

public interface ICurrentAdmin
{
    bool IsAuthenticated { get; }
    Guid AdminId { get; }
    string? Ip { get; }
}

public interface IPlatformRepository
{
    Task<PlatformAdmin?> AdminByEmailAsync(string email, CancellationToken ct = default);
    Task<PlatformAdmin?> AdminAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformAdmin>> AdminsAsync(CancellationToken ct = default);
    void AddAdmin(PlatformAdmin admin);

    Task<IReadOnlyList<PlatformPlan>> PlansAsync(CancellationToken ct = default);
    Task<PlatformPlan?> PlanAsync(Guid id, CancellationToken ct = default);
    void AddPlan(PlatformPlan plan);

    Task<IReadOnlyList<Organization>> OrganizationsAsync(CancellationToken ct = default);
    Task<Organization?> OrganizationAsync(Guid id, CancellationToken ct = default);
    Task<Dictionary<Guid, OrgStats>> StatsAsync(IReadOnlyCollection<Guid> orgIds, DateTime sinceUtc, CancellationToken ct = default);
    void AddOrganization(Organization organization);

    Task<IReadOnlyList<Store>> StoresAsync(Guid orgId, CancellationToken ct = default);
    Task<Store?> StoreAsync(Guid orgId, Guid storeId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> RegisterCountsAsync(IReadOnlyCollection<Guid> storeIds, CancellationToken ct = default);
    Task<bool> JoinCodeExistsAsync(string code, CancellationToken ct = default);
    void AddStore(Store store);
    /// <summary>Жаңа дүкеннің әдепкілері: ұйымның өлшем бірліктері (жоқ болса) және «Касса №1».</summary>
    Task EnsureStoreDefaultsAsync(Guid orgId, Guid storeId, CancellationToken ct = default);

    Task<IReadOnlyList<StaffUser>> StaffAsync(Guid orgId, CancellationToken ct = default);
    Task<StaffUser?> StaffUserAsync(Guid orgId, Guid id, CancellationToken ct = default);
    Task<bool> StaffPhoneExistsAsync(string phone, CancellationToken ct = default);
    void AddStaff(StaffUser user);
    void AddMembership(StoreMembership membership);
    Task RevokeStaffSessionsAsync(Guid staffUserId, CancellationToken ct = default);

    Task<(IReadOnlyList<PlatformPayment> Rows, int Total, decimal Sum)> PaymentsAsync(PlatformPaymentQuery q, CancellationToken ct = default);
    Task<PlatformPayment?> PaymentAsync(Guid id, CancellationToken ct = default);
    Task<DateOnly?> PaidUntilAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<(DateOnly PaidOn, decimal Amount)>> PaymentsSinceAsync(DateOnly from, CancellationToken ct = default);
    void AddPayment(PlatformPayment payment);

    Task<Dictionary<Guid, string>> AdminNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    void AddAudit(PlatformAuditEntry entry);
    Task<IReadOnlyList<PlatformAuditEntry>> AuditAsync(Guid? orgId, int take, CancellationToken ct = default);
}
