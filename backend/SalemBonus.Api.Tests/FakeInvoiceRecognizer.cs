using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Api.Tests;

/// <summary>
/// Тесттегі «Claude»: желіге шықпайды, тест не қойса соны қайтарады. Бір данасы бүкіл хостқа ортақ
/// (singleton) — сондықтан тану тесттері бірінен кейін бірі орындалады (бір сыныпта).
/// </summary>
public sealed class FakeInvoiceRecognizer : IInvoiceRecognizer
{
    public bool IsConfigured { get; set; } = true;
    public RecognizedInvoice? Next { get; set; }
    public Exception? Throw { get; set; }
    public string? LastContentType { get; private set; }
    public int LastLength { get; private set; }

    public Task<RecognizedInvoice> RecognizeAsync(byte[] content, string contentType, CancellationToken ct = default)
    {
        LastContentType = contentType;
        LastLength = content.Length;
        if (Throw is { } ex) throw ex;
        return Task.FromResult(Next ?? new RecognizedInvoice(null, null, null, []));
    }
}
