using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Rendering;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Tests.TestSupport;
using System.Text;

namespace CoverageReportGenerator.Core.Tests;

/// <summary>
/// レポートモデル生成とHTML描画のテスト。
/// </summary>
[TestClass]
public sealed class ReportGenerationTests
{
    /// <summary>
    /// 行状態がCovered/Uncovered/NoDataへ集約されることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Report_builder_marks_lines_as_covered_uncovered_or_no_data_without_partial_status()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Index.cshtml.cs", """
            public class IndexModel
            {
                public void OnGet()
                {
                    var covered = 1;
                    var mixed = 2; var alsoMixed = 3;
                    var uncovered = 4;
                }
            }
            """);
        var xml = """
            <Root CoveredStatements="2" TotalStatements="4" CoveragePercent="50">
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="IndexModel">
                    <Method Name="OnGet():System.Void">
                      <Statement FileIndex="1" Line="5" Covered="True" />
                      <Statement FileIndex="1" Line="6" Covered="False" />
                      <Statement FileIndex="1" Line="6" Covered="True" />
                      <Statement FileIndex="1" Line="7" Covered="False" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);

        Assert.AreEqual(1, report.Files.Count);
        var file = report.Files[0];
        Assert.AreEqual(LineCoverageStatus.Covered, file.Lines.Single(line => line.LineNumber == 5).Status);
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 6).Status);
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 7).Status);
        Assert.AreEqual(LineCoverageStatus.NoData, file.Lines.Single(line => line.LineNumber == 8).Status);
        Assert.AreEqual(2, report.Summary.CoveredStatements);
        Assert.AreEqual(4, report.Summary.TotalStatements);
    }

    /// <summary>
    /// プロジェクト全体の割合表示にRootのCoveragePercentを使うことを検証する。
    /// </summary>
    [TestMethod]
    public async Task Report_builder_uses_root_coverage_percent_for_project_summary()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Index.cshtml.cs", """
            public class IndexModel
            {
                public void OnGet()
                {
                    var covered = 1;
                }
            }
            """);
        var xml = """
            <Root CoveredStatements="1" TotalStatements="3" CoveragePercent="33">
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web" CoveredStatements="1" TotalStatements="3" CoveragePercent="33">
                <Namespace Name="Sample.Pages" CoveredStatements="1" TotalStatements="3" CoveragePercent="33">
                  <Type Name="IndexModel" CoveredStatements="1" TotalStatements="3" CoveragePercent="33">
                    <Method Name="OnGet():System.Void" CoveredStatements="1" TotalStatements="3" CoveragePercent="33">
                      <Statement FileIndex="1" Line="5" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);

        Assert.AreEqual(1, report.Summary.CoveredStatements);
        Assert.AreEqual(3, report.Summary.TotalStatements);
        Assert.AreEqual(33, report.Summary.CoveragePercent);
    }

    /// <summary>
    /// dotCover公式HTMLに合わせ、record主コンストラクタはソース行ハイライトから除外することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Report_builder_does_not_highlight_record_primary_constructor_statement_lines()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Models\CheckoutRequest.cs", """
            public sealed record CheckoutRequest(
                int ProductId,
                int Quantity,
                string? CouponCode);
            """);
        var xml = """
            <Root CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
              <FileIndices><File Index="1" Name="Models\CheckoutRequest.cs" /></FileIndices>
              <Assembly Name="Sample.Web" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                <Namespace Name="Sample.Models" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                  <Type Name="CheckoutRequest" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                    <Method Name=".ctor(System.Int32,System.Int32,System.String):System.Void" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                      <Statement FileIndex="1" Line="1" Column="22" EndLine="4" EndColumn="26" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);

        var file = report.Files.Single(item => item.RelativePath == @"Models\CheckoutRequest.cs");
        Assert.AreEqual(1, file.Summary.CoveredStatements);
        Assert.AreEqual(1, file.Summary.TotalStatements);
        Assert.IsTrue(file.Lines.All(line => line.Status == LineCoverageStatus.NoData));
    }

    /// <summary>
    /// Method集計値とStatement要素数が異なる場合にMethod集計値を優先することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Report_builder_prefers_dotcover_method_metrics_when_statement_nodes_are_duplicated()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Products\Details.cshtml.cs", """
            public class DetailsModel
            {
                public void OnGet()
                {
                    var items = new[] { 1, 2, 3 }
                        .Where(item => item > 1)
                        .ToList();
                }
            }
            """);
        var xml = """
            <Root CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
              <FileIndices><File Index="1" Name="Pages\Products\Details.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web" CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
                <Namespace Name="Sample.Pages.Products" CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
                  <Type Name="DetailsModel" CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
                    <Method Name="OnGet():System.Void" CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
                      <Statement FileIndex="1" Line="5" Column="21" EndLine="7" EndColumn="30" Covered="False" />
                      <Statement FileIndex="1" Line="5" Column="21" EndLine="7" EndColumn="30" Covered="False" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);

        Assert.AreEqual(0, report.Summary.CoveredStatements);
        Assert.AreEqual(1, report.Summary.TotalStatements);
        Assert.AreEqual(1, report.Files.Single().Summary.TotalStatements);
        Assert.AreEqual(1, report.Tree.Single().Summary.TotalStatements);
    }

    /// <summary>
    /// 複数行Statementの全行が同じカバレッジ状態になることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Report_builder_marks_all_lines_in_multiline_dotcover_statement()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Checkout.cs", """
            public class Checkout
            {
                public string Format()
                {
                    return string.Join(
                        ",",
                        new[] { "a", "b" });
                }
            }
            """);
        var xml = """
            <Root CoveredStatements="0" TotalStatements="1" CoveragePercent="0">
              <FileIndices><File Index="1" Name="Pages\Checkout.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="Checkout">
                    <Method Name="Format():System.String">
                      <Statement FileIndex="1" Line="5" Column="16" EndLine="7" EndColumn="39" Covered="False" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);

        var file = report.Files.Single(item => item.RelativePath == @"Pages\Checkout.cs");
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 5).Status);
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 6).Status);
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 7).Status);
        Assert.AreEqual(LineCoverageStatus.NoData, file.Lines.Single(line => line.LineNumber == 8).Status);
        Assert.AreEqual(0, report.Summary.CoveredStatements);
        Assert.AreEqual(1, report.Summary.TotalStatements);
    }

    /// <summary>
    /// フォルダ範囲がツリー、ランキング、ファイル、ソースへ反映されることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Folder_scope_report_includes_only_selected_folder_in_tree_rankings_files_and_sources()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Admin\Edit.cshtml.cs", """
            namespace Sample.Pages.Admin;
            public class EditModel { public void OnPost() { var x = 1; } }
            """);
        workspace.Write(@"Pages\Public\Index.cshtml.cs", """
            namespace Sample.Pages.Public;
            public class IndexModel { public void OnGet() { var x = 1; } }
            """);
        var xml = """
            <Root>
              <FileIndices>
                <File Index="1" Name="Pages\Admin\Edit.cshtml.cs" />
                <File Index="2" Name="Pages\Public\Index.cshtml.cs" />
              </FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages.Admin">
                  <Type Name="EditModel"><Method Name="OnPost():System.Void"><Statement FileIndex="1" Line="2" Covered="False" /></Method></Type>
                </Namespace>
                <Namespace Name="Sample.Pages.Public">
                  <Type Name="IndexModel"><Method Name="OnGet():System.Void"><Statement FileIndex="2" Line="2" Covered="True" /></Method></Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml, CoverageScopeType.Folder, workspace.PathOf(@"Pages\Admin"));

        Assert.AreEqual(1, report.Files.Count);
        Assert.AreEqual(@"Pages\Admin\Edit.cshtml.cs", report.Files[0].RelativePath);
        Assert.IsTrue(report.Tree.Flatten().Any(item => item.Kind == CoverageTreeKind.Namespace && item.Name == "Sample.Pages.Admin"));
        Assert.IsFalse(report.Tree.Flatten().Any(item => item.Name == "Sample.Pages.Public"));
        Assert.AreEqual(1, report.Rankings.LowestMembers.Count);
        Assert.AreEqual("OnPost", report.Rankings.LowestMembers[0].DisplayName);
    }

    /// <summary>
    /// ファイル範囲が他ファイルのStatementを除外し、選択ファイルだけで集計されることを検証する。
    /// </summary>
    [TestMethod]
    public async Task File_scope_report_includes_only_selected_file_in_summary_members_and_sources()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var edit = workspace.Write(@"Pages\Admin\Edit.cshtml.cs", """
            namespace Sample.Pages.Admin;
            public class EditModel
            {
                public void OnPost()
                {
                    var x = 1;
                }
            }
            """);
        workspace.Write(@"Pages\Admin\Delete.cshtml.cs", """
            namespace Sample.Pages.Admin;
            public class DeleteModel
            {
                public void OnPost()
                {
                    var x = 1;
                }
            }
            """);
        var xml = """
            <Root CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
              <FileIndices>
                <File Index="1" Name="Pages\Admin\Edit.cshtml.cs" />
                <File Index="2" Name="Pages\Admin\Delete.cshtml.cs" />
              </FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages.Admin">
                  <Type Name="EditModel"><Method Name="OnPost():System.Void"><Statement FileIndex="1" Line="6" Covered="False" /></Method></Type>
                  <Type Name="DeleteModel"><Method Name="OnPost():System.Void"><Statement FileIndex="2" Line="6" Covered="True" /></Method></Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml, CoverageScopeType.File, edit);

        Assert.AreEqual(1, report.Files.Count);
        Assert.AreEqual(@"Pages\Admin\Edit.cshtml.cs", report.Files[0].RelativePath);
        Assert.AreEqual(0, report.Summary.CoveredStatements);
        Assert.AreEqual(1, report.Summary.TotalStatements);
        Assert.AreEqual("OnPost", report.Members.Single().DisplayName);
        Assert.AreEqual(LineCoverageStatus.Uncovered, report.Files[0].Lines.Single(line => line.LineNumber == 6).Status);
        Assert.IsFalse(report.Files.Any(file => file.RelativePath == @"Pages\Admin\Delete.cshtml.cs"));
    }

    /// <summary>
    /// HTMLがランキング、ソースアンカー、現行タブ構成を出力することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Html_renderer_outputs_rankings_source_anchors_and_no_removed_tabs()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Index.cshtml.cs", """
            namespace Sample.Pages;
            public class IndexModel
            {
                public void OnGet()
                {
                    var x = 1;
                }
            }
            """);
        var xml = """
            <Root>
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="IndexModel"><Method Name="OnGet():System.Void"><Statement FileIndex="1" Line="6" Covered="False" /></Method></Type>
                </Namespace>
              </Assembly>
            </Root>
            """;
        var report = await BuildReportAsync(project, xml);

        var html = new HtmlReportRenderer().Render(report);

        StringAssert.Contains(html, "低カバレッジ メンバー");
        StringAssert.Contains(html, "coverage-file-row coverage-bad");
        StringAssert.Contains(html, "coverage-visual coverage-bad");
        StringAssert.Contains(html, "style=\"width:0%\"");
        StringAssert.Contains(html, "src-file-1-line-6");
        StringAssert.Contains(html, "jumpToSource(1, 4)");
        Assert.IsFalse(html.Contains("<th>ソース</th>", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<td>あり</td>", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<td>なし</td>", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("Partial", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("Raw", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("Coverage Tree", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(html.Contains("tab-tree", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// CP932ソースの日本語がHTMLで保持されることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Html_renderer_preserves_japanese_text_from_cp932_source_files()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var source = """
            namespace Sample.Pages;
            public class IndexModel
            {
                public void OnGet()
                {
                    // 日本語コメント
                }
            }
            """;
        workspace.WriteBytes(@"Pages\Index.cshtml.cs", Encoding.GetEncoding(932).GetBytes(source.ReplaceLineEndings(Environment.NewLine)));
        var xml = """
            <Root>
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="IndexModel"><Method Name="OnGet():System.Void"><Statement FileIndex="1" Line="6" Covered="True" /></Method></Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var report = await BuildReportAsync(project, xml);
        var html = new HtmlReportRenderer().Render(report);

        StringAssert.Contains(html, "日本語コメント");
    }

    private static async Task<CoverageReport> BuildReportAsync(
        string projectPath,
        string dotCoverXml,
        CoverageScopeType scopeType = CoverageScopeType.Project,
        string? scopePath = null)
    {
        var dotCover = new DotCoverDetailedXmlParser().Parse(dotCoverXml);
        var resolver = new ProjectSourceResolver();
        var snapshot = await resolver.ResolveAsync(projectPath);
        var members = new List<SourceMember>();
        var parser = new RoslynSourceMemberParser();
        foreach (var file in snapshot.SourceFiles.Where(file => file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            members.AddRange(await parser.ParseFileAsync(file.FullPath));
        }

        var analysis = new ProjectAnalysis(
            snapshot.ProjectPath,
            snapshot.ProjectName,
            snapshot.ProjectRoot,
            snapshot.SourceFiles,
            members,
            ProjectCacheStatus.Disabled);

        return new CoverageReportBuilder().Build(new CoverageReportRequest(
            analysis,
            dotCover,
            new CoverageSelection(scopeType, scopePath, "*.cs", "*.g.cs;bin;obj"),
            "Sample Coverage Report",
            DateTimeOffset.Parse("2026-06-18T10:00:00+09:00")));
    }
}
