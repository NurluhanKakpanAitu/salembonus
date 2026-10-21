using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalemBonus.Application.Notifications;

namespace SalemBonus.Infrastructure.Push;

/// <summary>Push кезегін фонда жібереді: бірнеше хабарлама қатар (тарату мыңдаған клиентке кетуі мүмкін).</summary>
public sealed class PushWorker(PushQueue queue, IServiceScopeFactory scopes, ILogger<PushWorker> log) : BackgroundService
{
    private const int Parallelism = 4;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Parallel.ForEachAsync(queue.ReadAllAsync(stoppingToken),
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = stoppingToken },
            async (id, ct) =>
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PushDispatcher>().SendAsync(id, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Бір хабарламаның қатесі кезекті тоқтатпайды.
                    log.LogError(ex, "Push хабарлама {Id} жіберілмеді", id);
                }
            });
}
