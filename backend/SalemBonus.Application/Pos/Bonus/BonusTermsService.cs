using System.Text;
using System.Text.RegularExpressions;
using SalemBonus.Application.Common.Exceptions;
using SalemBonus.Application.Common.Interfaces;
using SalemBonus.Application.Common.Localization;
using SalemBonus.Application.Pos.Catalog;
using SalemBonus.Domain.Core;
using SalemBonus.Domain.Pos.Bonus;

namespace SalemBonus.Application.Pos.Bonus;

/// <summary>
/// «Правила программы» (ТЗ «Бонусы» §9): бір черновик, бір жарияланған нұсқа, қалғаны — тарих. Жариялау жаңа нөмір береді, ескісі
/// мұрағатқа өтеді; «Восстановить» — ескі нұсқадан жаңа черновик (тарих өшпейді). Мәтін — рұқсат етілген HTML (тақырып, абзац, тізім,
/// жуан/көлбеу/асты сызылған, сілтеме) — қалғаны серверде тазаланады.
/// </summary>
public partial class BonusTermsService(IBonusRepository repo, BonusAccess access, IUnitOfWork unitOfWork)
{
    private AppLanguage Lang => access.Lang;
    private const int MaxLength = 100_000;

    public async Task<TermsStateDto> GetAsync(CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusView, ct);
        var all = await repo.TermsAsync(c.Store.Id, ct);
        var names = await repo.StaffNamesAsync(all.Select(t => t.CreatedBy).Concat(all.Where(t => t.PublishedBy != null).Select(t => t.PublishedBy!.Value)).Distinct().ToList(), ct);
        return new TermsStateDto(all.FirstOrDefault(t => t.Status == TermsStatus.Published) is { } p ? ToDto(p, names) : null,
            all.FirstOrDefault(t => t.Status == TermsStatus.Draft) is { } d ? ToDto(d, names) : null,
            all.Where(t => t.Status != TermsStatus.Draft).OrderByDescending(t => t.Version).Select(t => ToDto(t, names)).ToList(), Template(c.Store.Name));
    }

    public async Task<BonusTermsDto> SaveDraftAsync(SaveTermsDraftRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusTerms, ct);
        var content = Sanitize(r.Content);
        if (PlainText(content).Length == 0) throw new ValidationException(Messages.BonusTermsEmpty(Lang), "content");
        if (content.Length > MaxLength) throw new ValidationException(Messages.BonusTermsTooLong(Lang), "content");
        var draft = await repo.DraftForUpdateAsync(c.Store.Id, ct);
        if (draft is null)
        {
            draft = new BonusTerms { Id = Guid.NewGuid(), StoreId = c.Store.Id, Status = TermsStatus.Draft, CreatedBy = access.StaffUserId };
            repo.AddTerms(draft);
        }
        draft.Content = content;
        draft.Comment = CatalogAccess.Optional(r.Comment, 500);
        draft.UpdatedAt = DateTime.UtcNow;
        access.Audit(c, "bonus.terms.draft", "bonus_terms", draft.Id, null, new { Length = content.Length });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(draft, await repo.StaffNamesAsync([draft.CreatedBy], ct));
    }

    /// <summary>Жариялау (§9.2): черновик → жаңа нөмірлі жарияланған нұсқа (күшіне енетін күнімен), бұрынғы жарияланған — мұрағатта.</summary>
    public async Task<BonusTermsDto> PublishAsync(PublishTermsRequest r, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusTerms, ct);
        var terms = await unitOfWork.InTransactionAsync(async () =>
        {
            var draft = await repo.DraftForUpdateAsync(c.Store.Id, ct) ?? throw new ValidationException(Messages.BonusTermsNoDraft(Lang));
            if (await repo.PublishedForUpdateAsync(c.Store.Id, ct) is { } published)
            {
                published.Status = TermsStatus.Archived;
                published.UpdatedAt = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync(ct);
            }
            draft.Version = await repo.MaxTermsVersionAsync(c.Store.Id, ct) + 1;
            draft.Status = TermsStatus.Published;
            draft.PublishedAt = DateTime.UtcNow;
            draft.PublishedBy = access.StaffUserId;
            draft.EffectiveFrom = r.EffectiveFrom ?? c.Today;
            draft.Comment = CatalogAccess.Optional(r.Comment, 500) ?? draft.Comment;
            draft.UpdatedAt = DateTime.UtcNow;
            access.Audit(c, "bonus.terms.publish", "bonus_terms", draft.Id, null, new { draft.Version, draft.EffectiveFrom });
            await unitOfWork.SaveChangesAsync(ct);
            return draft;
        }, ct);
        return ToDto(terms, await repo.StaffNamesAsync([terms.CreatedBy, access.StaffUserId], ct));
    }

    /// <summary>Нұсқаны қалпына келтіру (§9.2): ескі мәтіннен жаңа черновик, тарих өзгермейді.</summary>
    public async Task<BonusTermsDto> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var c = await access.ContextAsync(StaffPermissions.BonusTerms, ct);
        var version = await repo.TermsVersionAsync(c.Store.Id, id, ct) ?? throw new NotFoundException(Messages.BonusTermsNotFound(Lang));
        var draft = await repo.DraftForUpdateAsync(c.Store.Id, ct);
        if (draft is null)
        {
            draft = new BonusTerms { Id = Guid.NewGuid(), StoreId = c.Store.Id, Status = TermsStatus.Draft, CreatedBy = access.StaffUserId };
            repo.AddTerms(draft);
        }
        draft.Content = version.Content;
        draft.BasedOnVersion = version.Version;
        draft.Comment = Lang == AppLanguage.Ru ? $"Восстановлено из версии {version.Version}" : $"{version.Version}-нұсқадан қалпына келтірілді";
        draft.UpdatedAt = DateTime.UtcNow;
        access.Audit(c, "bonus.terms.restore", "bonus_terms", draft.Id, null, new { From = version.Version });
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(draft, await repo.StaffNamesAsync([draft.CreatedBy], ct));
    }

    private static BonusTermsDto ToDto(BonusTerms t, IReadOnlyDictionary<Guid, string> names) => new(
        t.Id, t.Version, t.Content, t.Status.ToString(), t.Comment, t.EffectiveFrom, t.BasedOnVersion, names.GetValueOrDefault(t.CreatedBy, ""), t.UpdatedAt,
        t.PublishedAt, t.PublishedBy is { } p ? names.GetValueOrDefault(p) : null);

    /// <summary>Ұсынылатын құрылым (§9.3) — бастапқы черновик үшін; заң мәтінін тапсырыс беруші бекітеді.</summary>
    private string Template(string store) => Lang == AppLanguage.Ru
        ? $"<h2>Правила программы лояльности «{Esc(store)}»</h2><ol><li><b>Участники.</b> Участником становится любой покупатель, зарегистрированный по номеру телефона в кассе или приложении SalemBonus. Один клиент — один профиль.</li><li><b>Начисление.</b> Бонусы начисляются за покупки в процентах от оплаченной суммы; процент зависит от статуса клиента. На часть чека, оплаченную бонусами, бонусы не начисляются.</li><li><b>Использование.</b> Бонусами можно оплатить часть покупки в пределах лимита, установленного магазином. 1 бонус = 1 ₸.</li><li><b>Возвраты.</b> При возврате товара списанные бонусы возвращаются, начисленные за покупку — отменяются.</li><li><b>Срок действия.</b> Каждое начисление действует установленный срок; о скором сгорании магазин предупреждает в приложении.</li><li><b>День рождения.</b> Подарок ко дню рождения начисляется один раз в год при выполнении условий программы.</li><li><b>Корректировки.</b> Магазин вправе отменить бонусы, начисленные по ошибке, с указанием причины.</li><li><b>Персональные данные.</b> Данные используются только для работы программы; от рассылок можно отказаться.</li><li><b>Контакты.</b> Вопросы по программе — у кассира или по телефону магазина.</li></ol>"
        : $"<h2>«{Esc(store)}» адалдық бағдарламасының ережелері</h2><ol><li><b>Қатысушылар.</b> Кассада не SalemBonus қосымшасында телефонмен тіркелген кез келген сатып алушы. Бір клиент — бір профиль.</li><li><b>Есептеу.</b> Бонус төленген сомадан пайызбен есептеледі; пайыз клиент мәртебесіне байланысты. Бонуспен төленген бөлігіне бонус есептелмейді.</li><li><b>Қолдану.</b> Бонуспен сатып алудың дүкен белгілеген шегіндегі бөлігін төлеуге болады. 1 бонус = 1 ₸.</li><li><b>Қайтарым.</b> Тауар қайтарылса, шегерілген бонус қайтарылады, есептелгені алынады.</li><li><b>Мерзімі.</b> Әр есептеу белгіленген мерзімде жарамды; жанар алдында қосымшада ескертеміз.</li><li><b>Туған күн.</b> Туған күн сыйлығы шарттар орындалса жылына бір рет беріледі.</li><li><b>Түзетулер.</b> Қате есептелген бонусты дүкен себебін көрсетіп алып тастай алады.</li><li><b>Дербес деректер.</b> Тек бағдарлама үшін қолданылады; таратулардан бас тартуға болады.</li><li><b>Байланыс.</b> Сұрақтар — кассирде не дүкен телефонында.</li></ol>";

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ---------- HTML тазалау ----------

    private static readonly HashSet<string> AllowedTags = ["p", "br", "b", "strong", "i", "em", "u", "h2", "h3", "ul", "ol", "li", "a", "div", "span"];

    [GeneratedRegex("<(/?)([a-zA-Z0-9]+)([^>]*)>|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex("href\\s*=\\s*(\"([^\"]*)\"|'([^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    /// <summary>
    /// Рұқсат етілген тегтер ғана, атрибуттары алынады (сілтемеде тек http/https/mailto/tel href); қалғаны — мәтін ретінде экрандалады.
    /// Скрипт, стиль, оқиға атрибуттары өтпейді.
    /// </summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var sb = new StringBuilder();
        var pos = 0;
        foreach (Match m in Tag().Matches(html))
        {
            sb.Append(EscapeText(html[pos..m.Index]));
            pos = m.Index + m.Length;
            if (m.Value.StartsWith("<!--")) continue;
            var name = m.Groups[2].Value.ToLowerInvariant();
            if (!AllowedTags.Contains(name)) continue;
            var closing = m.Groups[1].Value == "/";
            if (name == "div") name = "p";
            if (name == "span") continue;
            if (closing) { if (name != "br") sb.Append($"</{name}>"); continue; }
            if (name == "a")
            {
                var href = Href().Match(m.Groups[3].Value) is { Success: true } h ? (h.Groups[2].Success ? h.Groups[2].Value : h.Groups[3].Value).Trim() : "";
                sb.Append(href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                          || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
                    ? $"<a href=\"{Esc(System.Net.WebUtility.HtmlDecode(href))}\" target=\"_blank\" rel=\"noopener noreferrer\">"
                    : "<a>");
                continue;
            }
            sb.Append(name == "br" ? "<br>" : $"<{name}>");
        }
        sb.Append(EscapeText(html[pos..]));
        return sb.ToString().Trim();
    }

    /// <summary>Тегтер арасындағы мәтін: бар сущностьтар сақталады, жалаң «&lt;» «&gt;» экрандалады.</summary>
    private static string EscapeText(string s) => s.Replace("<", "&lt;").Replace(">", "&gt;");

    public static string PlainText(string html) => System.Net.WebUtility.HtmlDecode(Tag().Replace(html, " ")).Trim();
}
