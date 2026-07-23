using ClosedXML.Excel;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Rendering;

/// <summary>
/// ファイル単位のカバレッジレポートをExcelブックへ描画する。
/// </summary>
public sealed class ExcelFileCoverageReportRenderer
{
    private static readonly XLColor CoveredColor = XLColor.FromHtml("#ABD98D");
    private static readonly XLColor UncoveredColor = XLColor.FromHtml("#E4E8EB");
    private static readonly XLColor HeaderFillColor = XLColor.FromHtml("#F2F4F8");
    private static readonly XLColor TitleFillColor = XLColor.FromHtml("#D7DBDE");
    private static readonly XLColor WarningColor = XLColor.FromHtml("#F0C66A");
    private static readonly XLColor DangerColor = XLColor.FromHtml("#DF7D7D");
    private static readonly XLColor NoDataColor = XLColor.FromHtml("#B8C0CA");

    /// <summary>
    /// レポートをExcelファイルへ書き出す。
    /// </summary>
    public void RenderToFile(CoverageReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        RenderToFile([report], outputPath);
    }

    /// <summary>
    /// 複数ファイルのレポートをシートごとに書き出す。
    /// </summary>
    public void RenderToFile(IReadOnlyList<CoverageReport> reports, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (reports.Count == 0)
        {
            throw new ArgumentException("Excel export requires at least one selected file report.", nameof(reports));
        }

        if (reports.Any(report => report.Files.Count != 1))
        {
            throw new ArgumentException("Excel export requires each report to contain a single selected file.", nameof(reports));
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        {
            var file = report.Files[0];
            var worksheet = workbook.Worksheets.Add(SheetNameFor(file.RelativePath, usedSheetNames, reports.Count == 1));
            RenderWorksheet(worksheet, report, file);
        }

        workbook.SaveAs(outputPath);
    }

    private static string SheetNameFor(string relativePath, ISet<string> usedSheetNames, bool singleSheet)
    {
        var baseName = singleSheet ? "Coverage" : Path.GetFileName(relativePath);
        baseName = SanitizeSheetName(baseName);
        var sheetName = TruncateSheetName(baseName);
        var suffix = 2;
        while (!usedSheetNames.Add(sheetName))
        {
            var marker = $" ({suffix})";
            sheetName = TruncateSheetName(baseName, marker);
            suffix++;
        }

        return sheetName;
    }

    private static string SanitizeSheetName(string value)
    {
        var invalidChars = new HashSet<char>(['[', ']', ':', '*', '?', '/', '\\']);
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim('\'');
        return string.IsNullOrWhiteSpace(sanitized) ? "Coverage" : sanitized;
    }

    private static string TruncateSheetName(string value, string suffix = "")
    {
        const int maxLength = 31;
        var available = maxLength - suffix.Length;
        var prefix = value.Length <= available ? value : value[..available];
        return prefix + suffix;
    }

    private static void RenderWorksheet(IXLWorksheet worksheet, CoverageReport report, FileCoverageReport file)
    {
        worksheet.Cell(1, 1).Value = "カバレッジレポート";
        worksheet.Range(1, 1, 1, 7).Merge().Style
            .Font.SetBold().Font.SetFontSize(16)
            .Fill.SetBackgroundColor(TitleFillColor);

        worksheet.Cell(3, 1).Value = "ファイル";
        worksheet.Cell(3, 2).Value = file.RelativePath;
        worksheet.Cell(4, 1).Value = "カバレッジ";
        WriteCoverageCell(worksheet.Cell(4, 2), file.Summary);
        WriteCoverageBarCell(worksheet.Cell(4, 3), file.Summary);
        worksheet.Cell(5, 1).Value = "Statement";
        worksheet.Cell(5, 2).Value = $"{file.Summary.CoveredStatements}/{file.Summary.TotalStatements}";
        worksheet.Range(3, 1, 5, 1).Style.Font.SetBold();
        worksheet.Range(3, 1, 5, 7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(3, 1, 5, 7).Style.Border.OutsideBorderColor = XLColor.FromHtml("#D7DDE5");

        var row = 7;
        worksheet.Cell(row, 1).Value = "メンバー一覧";
        worksheet.Range(row, 1, row, 7).Merge().Style.Font.SetBold();
        row++;
        Header(worksheet, row, ["メンバー", "カバレッジ", "バー", "Statement", "行", "種別", "クラス"]);

        var members = report.Members
            .Where(member => PathUtilities.PathComparer.Equals(member.FilePath, file.FullPath))
            .OrderBy(member => member.StartLine)
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var memberRows = new List<(MemberCoverageReport Member, int Row)>();

        foreach (var member in members)
        {
            row++;
            worksheet.Cell(row, 1).Value = member.DisplayName;
            WriteCoverageCell(worksheet.Cell(row, 2), member.Summary);
            WriteCoverageBarCell(worksheet.Cell(row, 3), member.Summary);
            worksheet.Cell(row, 4).Value = $"{member.Summary.CoveredStatements}/{member.Summary.TotalStatements}";
            worksheet.Cell(row, 5).Value = $"{member.StartLine}-{member.EndLine}";
            worksheet.Cell(row, 6).Value = MemberKindLabel(member.Kind.ToString());
            worksheet.Cell(row, 7).Value = member.ContainingType;
            ApplyCoverageRowStyle(worksheet.Range(row, 1, row, 7), member.Summary);
            memberRows.Add((member, row));
        }

        row += 2;
        worksheet.Cell(row, 1).Value = "ソース";
        worksheet.Range(row, 1, row, 2).Merge().Style.Font.SetBold();
        row++;
        Header(worksheet, row, ["行番号", "未カバー", "本文"]);
        row++;

        var sourceRowsByLine = new Dictionary<int, int>();
        foreach (var line in file.Lines)
        {
            sourceRowsByLine[line.LineNumber] = row;
            worksheet.Cell(row, 1).Value = line.LineNumber;
            if (line.Status == LineCoverageStatus.Uncovered)
            {
                worksheet.Cell(row, 2).Value = "※";
            }

            worksheet.Cell(row, 3).Value = line.Text;
            worksheet.Cell(row, 3).Style.Font.FontName = "Consolas";
            ApplyLineStyle(worksheet.Range(row, 1, row, 3), line.Status);
            row++;
        }

        AddMemberSourceLinks(worksheet, memberRows, sourceRowsByLine);
        ApplyWorksheetStyle(worksheet);
    }

    private static void AddMemberSourceLinks(
        IXLWorksheet worksheet,
        IReadOnlyList<(MemberCoverageReport Member, int Row)> memberRows,
        IReadOnlyDictionary<int, int> sourceRowsByLine)
    {
        foreach (var (member, row) in memberRows)
        {
            if (!sourceRowsByLine.TryGetValue(member.StartLine, out var sourceRow))
            {
                continue;
            }

            var memberCell = worksheet.Cell(row, 1);
            memberCell.SetHyperlink(new XLHyperlink(worksheet.Cell(sourceRow, 3), $"{member.StartLine}行へ移動"));
        }
    }

    private static void ApplyWorksheetStyle(IXLWorksheet worksheet)
    {
        worksheet.Columns(1, 7).AdjustToContents();
        EnsureColumnWidth(worksheet.Column(1), 18, 80);
        EnsureColumnWidth(worksheet.Column(2), 10, 20);
        EnsureColumnWidth(worksheet.Column(3), 140, 180);
        EnsureColumnWidth(worksheet.Column(4), 12, 20);
        EnsureColumnWidth(worksheet.Column(5), 18, 24);
        EnsureColumnWidth(worksheet.Column(6), 14, 24);
        EnsureColumnWidth(worksheet.Column(7), 22, 80);
        worksheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void EnsureColumnWidth(IXLColumn column, double minimumWidth, double maximumWidth)
    {
        column.Width = Math.Clamp(column.Width, minimumWidth, maximumWidth);
    }

    private static void Header(IXLWorksheet worksheet, int row, IReadOnlyList<string> labels)
    {
        for (var index = 0; index < labels.Count; index++)
        {
            worksheet.Cell(row, index + 1).Value = labels[index];
        }

        worksheet.Range(row, 1, row, labels.Count).Style
            .Font.SetBold()
            .Fill.SetBackgroundColor(HeaderFillColor);
    }

    private static void ApplyLineStyle(IXLRange range, LineCoverageStatus status)
    {
        if (status == LineCoverageStatus.Covered)
        {
            range.Style.Fill.SetBackgroundColor(CoveredColor);
        }
        else if (status == LineCoverageStatus.Uncovered)
        {
            range.Style.Fill.SetBackgroundColor(UncoveredColor);
            range.Cell(1, 2).Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
        }
    }

    private static void WriteCoverageCell(IXLCell cell, CoverageSummary summary)
    {
        cell.Value = summary.CoveragePercent / 100m;
        cell.Style.NumberFormat.Format = "0.0%";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        cell.Style.Font.SetBold();
        cell.Style.Fill.SetBackgroundColor(CoverageLevelColor(summary));
    }

    private static void WriteCoverageBarCell(IXLCell cell, CoverageSummary summary)
    {
        cell.Value = CoverageBar(summary);
        cell.Style.Font.FontName = "Consolas";
        cell.Style.Font.SetFontColor(CoverageLevelColor(summary));
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private static void ApplyCoverageRowStyle(IXLRange range, CoverageSummary summary)
    {
        range.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
        range.Style.Border.LeftBorderColor = CoverageLevelColor(summary);
    }

    private static string CoverageBar(CoverageSummary summary)
    {
        const int barLength = 10;
        if (summary.TotalStatements == 0)
        {
            return new string('□', barLength);
        }

        var filled = (int)Math.Round(Math.Clamp(summary.CoveragePercent, 0m, 100m) / 100m * barLength, MidpointRounding.AwayFromZero);
        return new string('■', filled) + new string('□', barLength - filled);
    }

    private static XLColor CoverageLevelColor(CoverageSummary summary)
    {
        if (summary.TotalStatements == 0)
        {
            return NoDataColor;
        }

        return summary.CoveragePercent switch
        {
            >= 80m => CoveredColor,
            >= 50m => WarningColor,
            _ => DangerColor
        };
    }

    private static string MemberKindLabel(string kind)
    {
        return kind switch
        {
            "Method" => "メソッド",
            "Constructor" => "コンストラクタ",
            "Property" => "プロパティ",
            "Accessor" => "アクセサ",
            "LocalFunction" => "ローカル関数",
            "Lambda" => "ラムダ",
            _ => kind
        };
    }
}
