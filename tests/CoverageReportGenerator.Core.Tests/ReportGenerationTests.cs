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
        Assert.AreEqual(LineCoverageStatus.Covered, file.Lines.Single(line => line.LineNumber == 6).Status);
        Assert.AreEqual(LineCoverageStatus.Uncovered, file.Lines.Single(line => line.LineNumber == 7).Status);
        Assert.AreEqual(LineCoverageStatus.NoData, file.Lines.Single(line => line.LineNumber == 8).Status);
        Assert.AreEqual(2, report.Summary.CoveredStatements);
        Assert.AreEqual(4, report.Summary.TotalStatements);
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

        StringAssert.Contains(html, "Lowest Members");
        StringAssert.Contains(html, "src-file-1-line-6");
        StringAssert.Contains(html, "jumpToSource(1, 4)");
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
