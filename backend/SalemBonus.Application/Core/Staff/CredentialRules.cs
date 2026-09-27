using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;

namespace SalemBonus.Application.Core.Staff;

/// <summary>Құпиясөз бен PIN ережелері — қалпына келтіруде де, ауыстыруда да бірдей.</summary>
public static class CredentialRules
{
    public static void ValidatePassword(string password, StaffAuthOptions opt, AppLanguage lang, string field = "newPassword")
    {
        if (password.Length < opt.MinPasswordLength)
            throw new ValidationException(Messages.StaffPasswordTooShort(lang, opt.MinPasswordLength), field);
        if (opt.RequireLetterAndDigit && !(password.Any(char.IsLetter) && password.Any(char.IsDigit)))
            throw new ValidationException(Messages.StaffPasswordWeak(lang), field);
    }

    /// <summary>
    /// ТЗ §17.8: дәл 4 цифр. Бұған қоса бірдей (1111) және қатарынан келетін (1234, 9876) цифрлар
    /// қабылданбайды — оларды бірінші болып теріп көреді.
    /// </summary>
    public static void ValidatePin(string pin, AppLanguage lang, string field = "pin")
    {
        if (pin.Length != 4 || !pin.All(char.IsAsciiDigit))
            throw new ValidationException(Messages.StaffPinFormat(lang), field);

        var d = pin.Select(c => c - '0').ToArray();
        var same = d.All(x => x == d[0]);
        var up = Enumerable.Range(1, 3).All(i => d[i] == d[i - 1] + 1);
        var down = Enumerable.Range(1, 3).All(i => d[i] == d[i - 1] - 1);
        if (same || up || down)
            throw new ValidationException(Messages.StaffPinSimple(lang), field);
    }
}
