using ClosedXML.Excel;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Tests.TestSupport;

namespace CoverageReportGenerator.Core.Tests;

/// <summary>
/// Excelレポート生成のテスト。
/// </summary>
[TestClass]
public sealed class ExcelReportGenerationTests
{
    /// <summary>
    /// 単一ファイルのソース、未カバー印、カバレッジバー、定義行へのソース行リンクがExcelへ出力されることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Excel_report_outputs_single_file_source_and_uncovered_marker()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var source = workspace.Write(@"Pages\Index.cshtml.cs", """
            public class IndexModel
            {
                public void OnGet()
                {
                    var covered = 1;
                    var uncovered = 2;
                }

                public void Invoke()
                {
                    OnGet();
                }
            }
            """);
        workspace.Write(@"Pages\Other.cshtml.cs", """
            public class OtherModel
            {
                public void OnGet()
                {
                    var other = 1;
                }
            }
            """);
        var xml = workspace.Write("dotcover.xml", """
            <Root CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
              <FileIndices>
                <File Index="1" Name="Pages\Index.cshtml.cs" />
                <File Index="2" Name="Pages\Other.cshtml.cs" />
              </FileIndices>
              <Assembly Name="Sample.Web" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                <Namespace Name="Sample.Pages" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                  <Type Name="IndexModel" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                    <Method Name="OnGet():System.Void" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                      <Statement FileIndex="1" Line="5" Covered="True" />
                      <Statement FileIndex="1" Line="6" Covered="False" />
                    </Method>
                  </Type>
                  <Type Name="OtherModel" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                    <Method Name="OnGet():System.Void" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                      <Statement FileIndex="2" Line="5" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """);
        var output = workspace.PathOf("index-coverage.xlsx");

        var result = await new ExcelReportGenerationService().GenerateAsync(new ExcelReportGenerationOptions(
            project,
            xml,
            source,
            output,
            "*.cs",
            "*.g.cs;bin;obj"));

        Assert.IsTrue(File.Exists(result.OutputPath));
        Assert.AreEqual(1, result.Report.Files.Count);
        Assert.AreEqual(@"Pages\Index.cshtml.cs", result.Report.Files[0].RelativePath);
        Assert.IsFalse(result.Report.Files.Any(file => file.RelativePath == @"Pages\Other.cshtml.cs"));
        using var workbook = new XLWorkbook(result.OutputPath);
        var worksheet = workbook.Worksheet("Coverage");
        Assert.AreEqual(0, worksheet.SheetView.SplitRow);
        Assert.IsFalse(worksheet.AutoFilter.IsEnabled);
        Assert.IsTrue(worksheet.Column(2).Width >= 10);
        Assert.IsTrue(worksheet.Column(3).Width >= 140);
        Assert.AreEqual("カバレッジレポート", worksheet.Cell(1, 1).GetString());
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.GetString().Contains('■')));
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.Address.ColumnNumber == 2 && cell.GetString() == "※"));
        var memberHeaderRow = worksheet.CellsUsed().Single(cell => cell.GetString() == "メンバー一覧").Address.RowNumber + 1;
        Assert.AreEqual("メンバー", worksheet.Cell(memberHeaderRow, 1).GetString());
        Assert.AreEqual("種別", worksheet.Cell(memberHeaderRow, 6).GetString());
        Assert.AreEqual("クラス", worksheet.Cell(memberHeaderRow, 7).GetString());
        var sourceHeaderRow = worksheet.CellsUsed().Single(cell => cell.Address.ColumnNumber == 1 && cell.GetString() == "ソース").Address.RowNumber + 1;
        Assert.AreEqual("行番号", worksheet.Cell(sourceHeaderRow, 1).GetString());
        Assert.AreEqual("未カバー", worksheet.Cell(sourceHeaderRow, 2).GetString());
        Assert.AreEqual("本文", worksheet.Cell(sourceHeaderRow, 3).GetString());
        var uncoveredSourceCell = worksheet.CellsUsed().Single(cell => cell.Address.ColumnNumber == 3 && cell.GetString().Contains("var uncovered = 2;"));
        Assert.AreEqual("6", worksheet.Cell(uncoveredSourceCell.Address.RowNumber, 1).GetString());
        Assert.AreEqual("※", worksheet.Cell(uncoveredSourceCell.Address.RowNumber, 2).GetString());
        var coveredSourceCell = worksheet.CellsUsed().Single(cell => cell.Address.ColumnNumber == 3 && cell.GetString().Contains("var covered = 1;"));
        var coveredMarkerCell = worksheet.Cell(coveredSourceCell.Address.RowNumber, 2);
        Assert.AreEqual(string.Empty, coveredMarkerCell.GetString());
        Assert.IsFalse(worksheet.CellsUsed(XLCellsUsedOptions.Contents).Any(cell =>
            cell.Address.RowNumber == coveredMarkerCell.Address.RowNumber &&
            cell.Address.ColumnNumber == coveredMarkerCell.Address.ColumnNumber));

        var memberCell = worksheet.Cell(memberHeaderRow + 1, 1);
        Assert.AreEqual("OnGet", memberCell.GetString());
        Assert.IsTrue(worksheet.Hyperlinks.TryGet(memberCell.Address, out var hyperlink));
        Assert.IsFalse(hyperlink.IsExternal);
        StringAssert.Contains(hyperlink.InternalAddress, "!C");
        Assert.AreEqual("3行へ移動", hyperlink.Tooltip);

        var targetAddress = hyperlink.InternalAddress.Split('!')[^1];
        var targetCell = worksheet.Cell(targetAddress);
        StringAssert.Contains(targetCell.GetString(), "public void OnGet()");
        Assert.IsFalse(targetCell.GetString().Contains("OnGet();", StringComparison.Ordinal));
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.Address.ColumnNumber == 3 && cell.GetString().Contains("OnGet();")));
    }

    /// <summary>
    /// 複数ファイルを選択した場合にファイルごとのシートを持つExcelブックを生成することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Excel_report_outputs_selected_files_as_separate_sheets()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var index = workspace.Write(@"Pages\Index.cshtml.cs", """
            public class IndexModel
            {
                public void OnGet()
                {
                    var index = 1;
                }
            }
            """);
        var details = workspace.Write(@"Pages\Products\Details.cshtml.cs", """
            public class DetailsModel
            {
                public void OnGet()
                {
                    var details = 1;
                }
            }
            """);
        workspace.Write(@"Pages\Products\Edit.cshtml.cs", """
            public class EditModel
            {
                public void OnGet()
                {
                    var edit = 1;
                }
            }
            """);
        var xml = workspace.Write("dotcover.xml", """
            <Root CoveredStatements="2" TotalStatements="3" CoveragePercent="66.7">
              <FileIndices>
                <File Index="1" Name="Pages\Index.cshtml.cs" />
                <File Index="2" Name="Pages\Products\Details.cshtml.cs" />
                <File Index="3" Name="Pages\Products\Edit.cshtml.cs" />
              </FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="IndexModel"><Method Name="OnGet():System.Void"><Statement FileIndex="1" Line="5" Covered="True" /></Method></Type>
                  <Type Name="DetailsModel"><Method Name="OnGet():System.Void"><Statement FileIndex="2" Line="5" Covered="False" /></Method></Type>
                  <Type Name="EditModel"><Method Name="OnGet():System.Void"><Statement FileIndex="3" Line="5" Covered="True" /></Method></Type>
                </Namespace>
              </Assembly>
            </Root>
            """);
        var output = workspace.PathOf("selected-coverage.xlsx");

        var result = await new ExcelReportGenerationService().GenerateAsync(new ExcelReportGenerationOptions(
            project,
            xml,
            [index, details],
            output,
            "*.cs",
            "*.g.cs;bin;obj"));

        Assert.AreEqual(2, result.Reports.Count);
        Assert.IsTrue(result.Reports.All(report => report.Files.Count == 1));
        using var workbook = new XLWorkbook(result.OutputPath);
        Assert.AreEqual(2, workbook.Worksheets.Count);
        var indexSheet = workbook.Worksheet("Index.cshtml.cs");
        var detailsSheet = workbook.Worksheet("Details.cshtml.cs");
        Assert.AreEqual(@"Pages\Index.cshtml.cs", indexSheet.Cell(3, 2).GetString());
        Assert.AreEqual(@"Pages\Products\Details.cshtml.cs", detailsSheet.Cell(3, 2).GetString());
        Assert.IsTrue(indexSheet.CellsUsed().Any(cell => cell.GetString().Contains("var index = 1;")));
        Assert.IsFalse(indexSheet.CellsUsed().Any(cell => cell.GetString().Contains("var details = 1;")));
        Assert.IsTrue(detailsSheet.CellsUsed().Any(cell => cell.GetString().Contains("var details = 1;")));
        Assert.IsFalse(workbook.Worksheets.Any(sheet => sheet.Name == "Edit.cshtml.cs"));
    }
}
