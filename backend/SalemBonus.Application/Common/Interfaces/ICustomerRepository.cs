using SalemBonus.Domain.Entities;

namespace SalemBonus.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task<Customer?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default);
    void Add(Customer customer);
    /// <summary>Клиенттің өтелмеген қарызы бар дүкендердің атаулары (SalemPos).</summary>
    Task<IReadOnlyList<string>> GetOpenDebtStoresAsync(Guid customerId, CancellationToken ct = default);
    /// <summary>
    /// Клиентті және оған қатысты барлық деректі біржола өшіреді: карталар, операциялар,
    /// хабарламалар, сеанстар, SMS кодтары, дүкендердің CRM карточкалары. Қайтару мүмкін емес.
    /// Дүкеннің есебіне керек жазбалар (чектер, өтелген қарыздар) қалады, бірақ адамға байланбайды:
    /// чектен клиент алынады, қарыз болса клиент жазбасы жеке деректерсіз «өшірілген» күйде қалады.
    /// </summary>
    Task DeleteAsync(Guid customerId, CancellationToken ct = default);
}
