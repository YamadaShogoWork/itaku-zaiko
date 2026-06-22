using ClosedXML.Excel;

var path = args[0];
using var workbook = new XLWorkbook(path);

foreach (var ws in workbook.Worksheets)
{
    Console.WriteLine($"=== Sheet: {ws.Name} (used range: {ws.RangeUsed()?.RangeAddress}) ===");
    var range = ws.RangeUsed();
    if (range == null) continue;

    foreach (var row in range.Rows())
    {
        foreach (var cell in row.Cells())
        {
            if (cell.IsEmpty()) continue;
            var addr = cell.Address.ToString();
            if (cell.HasFormula)
            {
                Console.WriteLine($"{addr}: FORMULA = {cell.FormulaA1}  => value = {cell.Value}");
            }
            else
            {
                Console.WriteLine($"{addr}: {cell.Value}");
            }
        }
    }
    Console.WriteLine();
}
