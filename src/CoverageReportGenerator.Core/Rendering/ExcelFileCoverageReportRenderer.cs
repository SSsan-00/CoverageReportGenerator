using ClosedXML.Excel;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Rendering;

/// <summary>
/// 単一ファイルのカバレッジレポートをExcelブックへ描画する。
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
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (report.Files.Count != 1)
        {
            throw new ArgumentException("Excel export requires a single selected file report.", nameof(report));
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Coverage");
        RenderWorksheet(worksheet, report, report.Files[0]);
        workbook.SaveAs(outputPath);
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
        Header(worksheet, row, ["種別", "クラス", "メンバー", "カバレッジ", "バー", "Statement", "行"]);

        var members = report.Members
            .Where(member => PathUtilities.PathComparer.Equals(member.FilePath, file.FullPath))
            .OrderBy(member => member.StartLine)
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var memberRows = new List<(MemberCoverageReport Member, int Row)>();

        foreach (var member in members)
        {
            row++;
            worksheet.Cell(row, 1).Value = MemberKindLabel(member.Kind.ToString());
            worksheet.Cell(row, 2).Value = member.ContainingType;
            worksheet.Cell(row, 3).Value = member.DisplayName;
            WriteCoverageCell(worksheet.Cell(row, 4), member.Summary);
            WriteCoverageBarCell(worksheet.Cell(row, 5), member.Summary);
            worksheet.Cell(row, 6).Value = $"{member.Summary.CoveredStatements}/{member.Summary.TotalStatements}";
            worksheet.Cell(row, 7).Value = $"{member.StartLine}-{member.EndLine}";
            ApplyCoverageRowStyle(worksheet.Range(row, 1, row, 7), member.Summary);
            memberRows.Add((member, row));
        }

        row += 2;
        worksheet.Cell(row, 1).Value = "ソース";
        worksheet.Range(row, 1, row, 2).Merge().Style.Font.SetBold();
        row++;
        Header(worksheet, row, ["未", "ソース"]);
        row++;

        var sourceStartRow = row;
        var sourceRowsByLine = new Dictionary<int, int>();
        foreach (var line in file.Lines)
        {
            sourceRowsByLine[line.LineNumber] = row;
            worksheet.Cell(row, 1).Value = line.Status == LineCoverageStatus.Uncovered ? "※" : string.Empty;
            worksheet.Cell(row, 2).Value = $"{line.LineNumber,4}: {line.Text}";
            worksheet.Cell(row, 2).Style.Font.FontName = "Consolas";
            ApplyLineStyle(worksheet.Range(row, 1, row, 2), line.Status);
            row++;
        }

        AddMemberSourceLinks(worksheet, memberRows, sourceRowsByLine);
        ApplyWorksheetStyle(worksheet, sourceStartRow);
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

            var memberCell = worksheet.Cell(row, 3);
            memberCell.SetHyperlink(new XLHyperlink(worksheet.Cell(sourceRow, 2), $"{member.StartLine}行へ移動"));
        }
    }

    private static void ApplyWorksheetStyle(IXLWorksheet worksheet, int sourceStartRow)
    {
        worksheet.Column(1).Width = 5;
        worksheet.Column(2).Width = 140;
        worksheet.Column(3).Width = 22;
        worksheet.Column(4).Width = 12;
        worksheet.Column(5).Width = 18;
        worksheet.Columns(6, 7).AdjustToContents();
        worksheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.SheetView.FreezeRows(sourceStartRow - 1);
        worksheet.RangeUsed()?.SetAutoFilter();
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
            range.FirstCell().Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
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
