using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Domain.Entities;
using SalemBonus.Domain.Enums;

namespace SalemBonus.Infrastructure.Push;

/// <summary>
/// Жаңа <see cref="Notification"/> қайдан жасалса да (бонус, туған күн, акция, жүйе) — базаға жазылған соң push кезегіне
/// түседі. Сыртқы транзакция ішінде сақталса, тек commit-тен кейін: кері қайтарылған сатылымның push-ы кетпейді.
/// Push кілттері мүлде жоқ болса, ештеңе кезекке қойылмайды.
/// </summary>
public sealed class PushNotificationInterceptor(PushQueue queue, IPushSender sender) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    // SaveChanges басталғанда қосылған хабарламалар (контекст бойынша), транзакция ішінде сақталғандары — commit-ке дейін.
    // Commit-сіз жабылған транзакцияның тізімі транзакциямен бірге жиналып кетеді.
    private readonly ConditionalWeakTable<DbContext, List<Guid>> _saving = new();
    private readonly ConditionalWeakTable<DbTransaction, List<Guid>> _uncommitted = new();

    private bool Enabled => sender.IsConfigured(PushPlatform.Android) || sender.IsConfigured(PushPlatform.Ios);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Saved(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Saved(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Committed(transaction);

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Committed(transaction);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _uncommitted.Remove(transaction);

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _uncommitted.Remove(transaction);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;
        _saving.Remove(context);
        if (!Enabled) return;
        var ids = context.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Id)
            .ToList();
        if (ids.Count > 0) _saving.Add(context, ids);
    }

    private void Saved(DbContext? context)
    {
        if (context is null || !_saving.TryGetValue(context, out var ids)) return;
        _saving.Remove(context);
        if (context.Database.CurrentTransaction is { } tx)
            _uncommitted.GetValue(tx.GetDbTransaction(), _ => []).AddRange(ids);
        else
            foreach (var id in ids) queue.Enqueue(id);
    }

    private void Forget(DbContext? context)
    {
        if (context is not null) _saving.Remove(context);
    }

    private void Committed(DbTransaction transaction)
    {
        if (!_uncommitted.TryGetValue(transaction, out var ids)) return;
        _uncommitted.Remove(transaction);
        foreach (var id in ids) queue.Enqueue(id);
    }
}
