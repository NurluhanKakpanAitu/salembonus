using System.Globalization;
using ClosedXML.Excel;
using SalemBonus.Application.Common.Interfaces;

namespace SalemBonus.Infrastructure.Storage;

public class XlsxSpreadsheet : ISpreadsheet
{
    public SpreadsheetData Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("Not an xlsx file", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidDataException("No worksheets");
            var used = sheet.RangeUsed();
            if (used is null) return new SpreadsheetData([], []);

            var lastColumn = used.LastColumn().ColumnNumber();
            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var headers = Enumerable.Range(1, lastColumn).Select(c => Text(sheet.Cell(firstRow, c))).ToList();
            var rows = new List<IReadOnlyList<string>>();
            for (var r = firstRow + 1; r <= lastRow; r++)
                rows.Add(Enumerable.Range(1, lastColumn).Select(c => Text(sheet.Cell(r, c))).ToList());
            return new SpreadsheetData(headers, rows);
        }
    }

    private static string Text(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return string.Empty;
        if (v.IsNumber) return v.GetNumber().ToString("0.############", CultureInfo.InvariantCulture);
        if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (v.IsBoolean) return v.GetBoolean() ? "true" : "false";
        if (v.IsText) return v.GetText().Trim();
        return cell.GetFormattedString().Trim();
    }

    public byte[] Write(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F1FF");
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case decimal d: cell.Value = d; break;
                    case int i: cell.Value = i; break;
                    // Мәтін ретінде: 0012345 штрихкоды санға айналып, нөлін жоғалтпасын.
                    case var other: cell.Value = other.ToString(); cell.Style.NumberFormat.Format = "@"; break;
                }
            }
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        for (var c = 1; c <= headers.Count; c++)
            sheet.Column(c).Width = Math.Clamp(headers[c - 1].Length + 4, 12, 40);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
