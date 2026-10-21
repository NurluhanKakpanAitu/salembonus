using System.Threading.Channels;

namespace SalemBonus.Infrastructure.Push;

/// <summary>
/// Жіберілуі тиіс хабарламалардың кезегі (жадта). Сұраныс FCM/APNs-ті күтпейді: хабарлама базаға жазылған соң
/// id-і осында түседі, PushWorker фонда жібереді. Сервер қайта қосылса, кезектегілер жоғалады — хабарлама
/// қосымшаның ішінде бәрібір тұр.
/// </summary>
public sealed class PushQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = false });

    public void Enqueue(Guid notificationId) => _channel.Writer.TryWrite(notificationId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
