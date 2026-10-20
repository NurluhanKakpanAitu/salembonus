using SalemBonus.Application.BonusCards;

namespace SalemBonus.Api;

/// <summary>
/// Мерзімі өткен бонустарды кезеңмен өшіреді. Сервер ұйқыға кетсе де,
/// оянған соң келесі айналымда бәрі өшеді — жану күні партияда жазулы тұр.
/// </summary>
public class BonusExpiryWorker(IServiceProvider services, ILogger<BonusExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var expiry = scope.ServiceProvider.GetRequiredService<IBonusExpiryService>();

                // Бір айналымда партия-партиямен өшіреміз.
                int burned;
                do
                {
                    burned = await expiry.BurnExpiredAsync(ct);
                    if (burned > 0) logger.LogInformation("Мерзімі өткен {Count} бонус партиясы өшірілді", burned);
                } while (burned > 0 && !ct.IsCancellationRequested);

                // «Бонусы»: туған күн сыйлықтары, жану ескертулері, жоспарланған таратулар, мәртебе мерзімдері (бәрі қайталауға қауіпсіз).
                var jobs = scope.ServiceProvider.GetRequiredService<SalemBonus.Application.Pos.Bonus.BonusJobs>();
                var r = await jobs.RunAsync(ct);
                if (r.Birthdays + r.Warnings + r.Campaigns > 0)
                    logger.LogInformation("Бонус тапсырмалары: туған күн {B}, ескерту {W}, тарату {C}", r.Birthdays, r.Warnings, r.Campaigns);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Бонус мерзімін тексеру сәтсіз аяқталды");
            }

            try
            {
                await Task.Delay(Interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
