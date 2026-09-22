namespace SalemBonus.Application.Customers.Dtos;

/// <summary>QR-ға салынатын мәтін: "SB:" префиксі + код. Касса префикс арқылы SalemBonus кодын таниды.</summary>
public record QrCodeDto(string Code, string Payload);
