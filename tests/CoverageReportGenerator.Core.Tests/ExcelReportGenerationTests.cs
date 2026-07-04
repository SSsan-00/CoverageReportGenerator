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
        var xml = workspace.Write("dotcover.xml", """
            <Root CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                <Namespace Name="Sample.Pages" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                  <Type Name="IndexModel" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                    <Method Name="OnGet():System.Void" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                      <Statement FileIndex="1" Line="5" Covered="True" />
                      <Statement FileIndex="1" Line="6" Covered="False" />
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
        using var workbook = new XLWorkbook(result.OutputPath);
        var worksheet = workbook.Worksheet("Coverage");
        Assert.AreEqual(0, worksheet.SheetView.SplitRow);
        Assert.IsTrue(worksheet.Column(2).Width >= 140);
        Assert.IsTrue(worksheet.Column(3).Width >= 22);
        Assert.AreEqual("カバレッジレポート", worksheet.Cell(1, 1).GetString());
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.GetString().Contains('■')));
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.Address.ColumnNumber == 1 && cell.GetString() == "※"));
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.Address.ColumnNumber == 2 && cell.GetString().Contains("var uncovered = 2;")));

        var memberCell = worksheet.CellsUsed().Single(cell => cell.GetString() == "OnGet");
        Assert.IsTrue(worksheet.Hyperlinks.TryGet(memberCell.Address, out var hyperlink));
        Assert.IsFalse(hyperlink.IsExternal);
        StringAssert.Contains(hyperlink.InternalAddress, "!B");
        Assert.AreEqual("3行へ移動", hyperlink.Tooltip);

        var targetAddress = hyperlink.InternalAddress.Split('!')[^1];
        var targetCell = worksheet.Cell(targetAddress);
        StringAssert.Contains(targetCell.GetString(), "public void OnGet()");
        Assert.IsFalse(targetCell.GetString().Contains("OnGet();", StringComparison.Ordinal));
        Assert.IsTrue(worksheet.CellsUsed().Any(cell => cell.Address.ColumnNumber == 2 && cell.GetString().Contains("OnGet();")));
    }
}
