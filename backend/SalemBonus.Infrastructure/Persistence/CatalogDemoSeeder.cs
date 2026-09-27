using Microsoft.EntityFrameworkCore;
using SalemBonus.Domain.Pos.Catalog;

namespace SalemBonus.Infrastructure.Persistence;

/// <summary>
/// Демо каталог — ТЕК Development ортасында, MKM AUTO (автобөлшектер) бизнесіне, макеттегідей.
/// Бизнесте бірде-бір санат болмаса ғана жасалады.
/// </summary>
public static class CatalogDemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var orgId = StaffSeeder.DemoOrganizationId;
        if (!await db.Organizations.AnyAsync(o => o.Id == orgId, ct)) return;

        await CatalogDefaults.EnsureUnitsAsync(db, orgId, ct);
        if (await db.CatalogNodes.AnyAsync(n => n.OrganizationId == orgId, ct))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var order = 0;
        CatalogNode Node(CatalogNode? parent, string name, string? icon = null)
        {
            var id = Guid.NewGuid();
            var node = new CatalogNode
            {
                Id = id,
                OrganizationId = orgId,
                ParentId = parent?.Id,
                Name = name,
                Icon = icon,
                SortOrder = ++order,
                Path = parent is null ? id.ToString() : $"{parent.Path}/{id}",
                Depth = parent is null ? 0 : parent.Depth + 1,
            };
            db.CatalogNodes.Add(node);
            return node;
        }

        var auto = Node(null, "Автозапчасти", "car");
        var brakes = Node(auto, "Тормозная система", "disc");
        foreach (var n in new[] { "Тормозные колодки", "Тормозные диски", "Тормозные барабаны", "Тормозные цилиндры",
                     "Тормозные шланги", "Тормозные жидкости", "Ремкомплекты", "Датчики ABS" })
            Node(brakes, n);
        var engine = Node(auto, "Двигатель", "cog");
        foreach (var n in new[] { "Свечи зажигания", "Ремни и цепи ГРМ", "Прокладки" }) Node(engine, n);
        var filters = Node(auto, "Фильтры", "filter");
        foreach (var n in new[] { "Масляные фильтры", "Воздушные фильтры", "Салонные фильтры", "Топливные фильтры" }) Node(filters, n);
        foreach (var (n, i) in new[] { ("Подвеска", "move-vertical"), ("Кузовные детали", "car-front"), ("Электрика", "zap"),
                     ("Охлаждение", "snowflake"), ("Трансмиссия", "settings-2"), ("Топливная система", "fuel"),
                     ("Рулевое управление", "circle-dot"), ("Сцепление", "link") })
            Node(auto, n, i);

        var oils = Node(null, "Масла и жидкости", "droplet");
        foreach (var n in new[] { "Моторные масла", "Трансмиссионные масла", "Антифризы", "Тормозные жидкости" }) Node(oils, n);
        Node(null, "Аккумуляторы", "battery");
        var tires = Node(null, "Шины и диски", "circle-dot");
        foreach (var n in new[] { "Летние шины", "Зимние шины", "Диски" }) Node(tires, n);
        Node(null, "Инструменты", "wrench");
        Node(null, "Аксессуары", "sparkles");
        Node(null, "Расходные материалы", "package");

        var brandOrder = 0;
        foreach (var name in new[] { "Bosch", "Toyota", "Hyundai", "Mobil", "Shell", "Castrol", "Michelin", "Bridgestone",
                     "Varta", "NGK", "MANN", "TRW", "KYB", "Brembo" })
            db.Brands.Add(new Brand { Id = Guid.NewGuid(), OrganizationId = orgId, Name = name, SortOrder = ++brandOrder });

        var charOrder = 0;
        void Characteristic(string name, CharacteristicType type, bool required, params string[] options)
        {
            var id = Guid.NewGuid();
            db.Characteristics.Add(new CharacteristicDefinition
            {
                Id = id,
                OrganizationId = orgId,
                Name = name,
                Type = type,
                IsRequired = required,
                SortOrder = ++charOrder,
                Options = options.Select((v, i) => new CharacteristicOption { Id = Guid.NewGuid(), DefinitionId = id, Value = v, SortOrder = i + 1 }).ToList(),
            });
        }
        // Міндетті сипаттама бизнестің барлық тауарына қойылады (майға да, шинаға да), сондықтан демода — жоқ.
        Characteristic("Производитель", CharacteristicType.List, false, "Bosch", "Brembo", "TRW", "ATE", "Ferodo");
        Characteristic("Модель авто", CharacteristicType.List, false, "Toyota", "Hyundai", "Kia", "Chevrolet");
        Characteristic("Год выпуска", CharacteristicType.Range, false);
        Characteristic("Сторона установки", CharacteristicType.List, false, "Передние", "Задние");
        Characteristic("Тип", CharacteristicType.List, false, "Дисковые", "Барабанные");
        Characteristic("Артикул (OEM)", CharacteristicType.Text, false);
        Characteristic("Материал", CharacteristicType.List, false, "Керамика", "Полуметалл", "Органика");
        Characteristic("Комплект", CharacteristicType.Number, false);
        Characteristic("Ширина (мм)", CharacteristicType.Number, false);

        await db.SaveChangesAsync(ct);
    }
}
