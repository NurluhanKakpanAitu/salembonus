namespace SalemBonus.Application.Notifications.Dtos;

/// <summary>Қосымша push токенін тіркейді: platform — android не ios, language — kk не ru (бос болса сұраныстың тілі).</summary>
public record RegisterDeviceRequest(string? Token, string? Platform, string? Language);

public record UnregisterDeviceRequest(string? Token);
