namespace SalemBonus.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Әрекетті бір транзакцияда орындау (мысалы, чек нөмірін алу мен сатылымды сақтау).
    /// Қате болса — бәрі кері қайтарылады.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
