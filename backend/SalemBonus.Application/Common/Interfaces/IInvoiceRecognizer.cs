namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Накладнаядан танылған бір жол: бәрі «қалай көрінсе солай», каталогпен әлі сәйкестендірілмеген.</summary>
public record RecognizedLine(string? Name, string? Article, string? Barcode, decimal? Quantity, decimal? Price, decimal? LineTotal);

/// <summary>Накладнаяның танылған мазмұны (ТЗ «Склад» §8). Күн — ISO «yyyy-MM-dd» немесе бос.</summary>
public record RecognizedInvoice(string? SupplierName, string? InvoiceNumber, string? Date, IReadOnlyList<RecognizedLine> Lines);

/// <summary>
/// Накладнаяның фотосын / PDF-ін оқып, жолдарын шығаратын тану қызметі (Claude vision). Сәйкестендіру мен тексеру
/// мұнда емес — <c>InvoiceRecognitionService</c>-те: танылған мәтін сенімсіз дерек, оны сервер қайта тексереді.
/// Тесттерде жалған іске асыру қойылады — желіге шықпайды.
/// </summary>
public interface IInvoiceRecognizer
{
    bool IsConfigured { get; }
    Task<RecognizedInvoice> RecognizeAsync(byte[] content, string contentType, CancellationToken ct = default);
}

/// <summary>Тану қызметі жауап бере алмады (желі, лимит, бас тарту): пайдаланушыға жалпы хабар, логқа — себебі.</summary>
public class InvoiceRecognitionException(string message, Exception? inner = null) : Exception(message, inner);
