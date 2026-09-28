using SalemBonus.Application.Core.Staff.Dtos;

namespace SalemBonus.Application.Core.Staff;

public interface IStaffAuthService
{
    Task<StaffSession> LoginAsync(StaffLoginRequest request, CancellationToken ct = default);
    Task<StaffSession> RefreshAsync(string? refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task<StaffMeDto> GetMeAsync(CancellationToken ct = default);
    Task<StaffMeDto> SetLanguageAsync(string language, CancellationToken ct = default);

    /// <summary>
    /// Тіркелген кассада PIN-мен кіру (кассир ауысуы, ТЗ «Касса» §15). Шақыратын жақ кассаны
    /// өзі тексереді; мұнда қызметкердің сол дүкенде сата алатыны және PIN тексеріледі.
    /// </summary>
    Task<StaffSession> SignInWithPinAsync(Guid staffUserId, string? pin, Guid storeId, Guid registerId, string? device, CancellationToken ct = default);

    /// <summary>Бұғатталған кассаны ашу: қазіргі қызметкердің PIN-і (ТЗ «Касса» §18).</summary>
    Task VerifyPinAsync(string? pin, Guid storeId, Guid registerId, CancellationToken ct = default);
    /// <summary>
    /// Басқа қызметкердің PIN-імен растау (мысалы, кассир шегінен асқан жеңілдік). Растаушының осы
    /// дүкенде <paramref name="permission"/> құқығы болуы керек; қате PIN бұғат есептегішін арттырады.
    /// Сәтті болса — ештеңе сақталмайды, шақырушы өз транзакциясымен сақтайды.
    /// </summary>
    Task ApproveWithPinAsync(Guid approverId, string? pin, Guid storeId, Guid registerId, string permission, CancellationToken ct = default);
}
