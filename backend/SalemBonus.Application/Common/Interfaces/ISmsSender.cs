namespace SalemBonus.Application.Common.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phone, string text, CancellationToken ct = default);
}
