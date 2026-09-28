namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Кесте файлының бірінші парағы: тақырыптар мен жолдар (барлық мән — мәтін).</summary>
public record SpreadsheetData(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Excel (.xlsx) оқу мен жазу. Сандар инвариант түрде («12.5»), күндер «yyyy-MM-dd» болып оқылады —
/// тексеру мен түрлендіру сервисте.
/// </summary>
public interface ISpreadsheet
{
    /// <summary>Файл оқылмаса — <see cref="InvalidDataException"/>.</summary>
    SpreadsheetData Read(Stream stream);

    /// <summary>Мән: string — мәтін (штрихкодтың алдыңғы нөлі сақталады), decimal/int — сан.</summary>
    byte[] Write(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);
}
