using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Tests.TestSupport;
using System.Text;

namespace CoverageReportGenerator.Core.Tests;

/// <summary>
/// プロジェクト解析とRoslynメンバー解析のテスト。
/// </summary>
[TestClass]
public sealed class ProjectAnalysisTests
{
    /// <summary>
    /// .csproj以外の入力を拒否することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Source_resolver_rejects_paths_that_are_not_csproj_files()
    {
        using var workspace = TestWorkspace.Create();
        var solution = workspace.Write("Sample.sln", string.Empty);

        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => new ProjectSourceResolver().ResolveAsync(solution));

        StringAssert.Contains(ex.Message, ".csproj");
    }

    /// <summary>
    /// Razor Pagesプロジェクトでフォルダ範囲を再帰的に選択できることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Source_resolver_reads_razor_project_files_and_applies_folder_scope_recursively()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        workspace.Write(@"Pages\Admin\Edit.cshtml.cs", "public class EditModel { public void OnGet() {} }");
        workspace.Write(@"Pages\Admin\Details.cshtml.cs", "public class DetailsModel { public void OnGet() {} }");
        workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");
        workspace.Write(@"obj\Generated.g.cs", "public class Generated {}");

        var resolver = new ProjectSourceResolver();
        var snapshot = await resolver.ResolveAsync(project);

        var selector = new CoverageTargetSelector();
        var selected = selector.Select(snapshot, new CoverageSelection(
            CoverageScopeType.Folder,
            workspace.PathOf(@"Pages\Admin"),
            "*.cs",
            "*.g.cs;bin;obj"));

        Assert.AreEqual(3, snapshot.SourceFiles.Count);
        Assert.AreEqual(2, selected.IncludedFiles.Count);
        foreach (var file in selected.IncludedFiles)
        {
            StringAssert.Contains(file.FullPath, @"\Pages\Admin\");
        }

        Assert.IsFalse(selected.IncludedFiles.Any(file => file.FullPath.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// フォルダ選択UI用のツリーに再帰的なファイル数とメンバー数が集計されることを検証する。
    /// </summary>
    [TestMethod]
    public void Folder_tree_builder_groups_sources_and_members_recursively()
    {
        using var workspace = TestWorkspace.Create();
        var adminEdit = workspace.PathOf(@"Pages\Admin\Edit.cshtml.cs");
        var adminDetails = workspace.PathOf(@"Pages\Admin\Details.cshtml.cs");
        var index = workspace.PathOf(@"Pages\Index.cshtml.cs");
        var service = workspace.PathOf(@"Services\CartService.cs");
        var analysis = new ProjectAnalysis(
            workspace.PathOf("Sample.Web.csproj"),
            "Sample.Web",
            workspace.Root,
            [
                new SourceFile(adminEdit, @"Pages\Admin\Edit.cshtml.cs", ".cs"),
                new SourceFile(adminDetails, @"Pages\Admin\Details.cshtml.cs", ".cs"),
                new SourceFile(index, @"Pages\Index.cshtml.cs", ".cs"),
                new SourceFile(service, @"Services\CartService.cs", ".cs")
            ],
            [
                new SourceMember(adminEdit, @"Pages\Admin\Edit.cshtml.cs", SourceMemberKind.Method, "EditModel", "OnGet", "OnGet", "OnGet()", 1, 3),
                new SourceMember(adminDetails, @"Pages\Admin\Details.cshtml.cs", SourceMemberKind.Method, "DetailsModel", "OnGet", "OnGet", "OnGet()", 1, 3),
                new SourceMember(service, @"Services\CartService.cs", SourceMemberKind.Method, "CartService", "Calculate", "Calculate", "Calculate()", 1, 3)
            ],
            ProjectCacheStatus.Valid);

        var root = new ProjectFolderTreeBuilder().Build(analysis);
        var pages = root.Children.Single(folder => folder.Name == "Pages");
        var admin = pages.Children.Single(folder => folder.Name == "Admin");
        var services = root.Children.Single(folder => folder.Name == "Services");

        Assert.AreEqual("Sample.Web", root.Name);
        Assert.AreEqual(string.Empty, root.RelativePath);
        Assert.AreEqual(4, root.SourceFileCount);
        Assert.AreEqual(3, root.MemberCount);
        Assert.AreEqual(3, pages.SourceFileCount);
        Assert.AreEqual(2, pages.MemberCount);
        Assert.AreEqual(2, admin.SourceFileCount);
        Assert.AreEqual(2, admin.MemberCount);
        Assert.AreEqual(workspace.PathOf(@"Pages\Admin"), admin.FullPath);
        Assert.AreEqual(1, services.SourceFileCount);
        Assert.AreEqual(1, services.MemberCount);
    }

    /// <summary>
    /// Compile Includeがあってもディレクトリ走査を継続することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Source_resolver_does_not_stop_directory_scanning_when_compile_include_exists()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <Compile Include="Linked\OnlyExplicit.cs" />
              </ItemGroup>
            </Project>
            """);
        workspace.Write(@"Linked\OnlyExplicit.cs", "public class OnlyExplicit { }");
        workspace.Write(@"Pages\Index.cshtml", "@page");
        workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");

        var snapshot = await new ProjectSourceResolver().ResolveAsync(project);

        Assert.IsTrue(snapshot.SourceFiles.Any(file => file.RelativePath == @"Linked\OnlyExplicit.cs"));
        Assert.IsTrue(snapshot.SourceFiles.Any(file => file.RelativePath == @"Pages\Index.cshtml"));
        Assert.IsTrue(snapshot.SourceFiles.Any(file => file.RelativePath == @"Pages\Index.cshtml.cs"));
    }

    /// <summary>
    /// Roslyn解析で主要なメンバー種別を検出できることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Roslyn_member_parser_finds_methods_properties_local_functions_and_lambdas()
    {
        using var workspace = TestWorkspace.Create();
        var source = workspace.Write(@"Pages\Edit.cshtml.cs", """
            using System;
            public class EditModel
            {
                public string? Name { get; set; }

                public async System.Threading.Tasks.Task OnPostAsync()
                {
                    void Validate() { }
                    var action = () => Validate();
                    await System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """);

        var members = await new RoslynSourceMemberParser().ParseFileAsync(source);

        Assert.IsTrue(members.Any(member => member.Kind == SourceMemberKind.Property && member.Name == "Name"));
        Assert.IsTrue(members.Any(member => member.Kind == SourceMemberKind.Method && member.Name == "OnPostAsync"));
        Assert.IsTrue(members.Any(member => member.Kind == SourceMemberKind.LocalFunction && member.Name == "Validate"));
        Assert.IsTrue(members.Any(member => member.Kind == SourceMemberKind.Lambda && member.DisplayName.Contains("lambda in OnPostAsync")));
    }

    /// <summary>
    /// Shift_JISのC#ソースをRoslyn解析できることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Roslyn_member_parser_reads_shift_jis_source_files()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var workspace = TestWorkspace.Create();
        var source = """
            public class IndexModel
            {
                public void 日本語メソッド()
                {
                }
            }
            """;
        var path = workspace.WriteBytes(@"Pages\Index.cshtml.cs", Encoding.GetEncoding(932).GetBytes(source.ReplaceLineEndings(Environment.NewLine)));

        var members = await new RoslynSourceMemberParser().ParseFileAsync(path);

        Assert.IsTrue(members.Any(member => member.Kind == SourceMemberKind.Method && member.Name == "日本語メソッド"));
    }

    /// <summary>
    /// 未変更ファイルの解析結果をキャッシュ再利用できることを検証する。
    /// </summary>
    [TestMethod]
    public async Task Project_cache_reuses_unchanged_member_analysis_and_rebuilds_changed_files()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var source = workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");
        var cacheDir = workspace.CreateDirectory("cache");

        var analyzer = new ProjectAnalyzer(new ProjectSourceResolver(), new RoslynSourceMemberParser(), new ProjectCacheService(cacheDir));
        var first = await analyzer.AnalyzeAsync(project);
        var second = await analyzer.AnalyzeAsync(project);

        Assert.AreEqual(ProjectCacheStatus.Created, first.CacheStatus);
        Assert.AreEqual(ProjectCacheStatus.Valid, second.CacheStatus);

        await File.AppendAllTextAsync(source, Environment.NewLine + "public class Extra { public void Run() {} }");
        var third = await analyzer.AnalyzeAsync(project);

        Assert.AreEqual(ProjectCacheStatus.Updated, third.CacheStatus);
        Assert.IsTrue(third.Members.Any(member => member.Name == "Run"));
    }

    /// <summary>
    /// 古いスキーマのキャッシュを無視することを検証する。
    /// </summary>
    [TestMethod]
    public async Task Project_cache_ignores_entries_from_previous_schema_versions()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var source = workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");
        var cacheDir = workspace.CreateDirectory("cache");
        var resolver = new ProjectSourceResolver();
        var cache = new ProjectCacheService(cacheDir);
        var snapshot = await resolver.ResolveAsync(project);
        var oldEntry = new ProjectCacheEntry(
            1,
            snapshot.ProjectPath,
            snapshot.ProjectName,
            snapshot.ProjectRoot,
            ProjectCacheService.Metadata(project),
            snapshot.SourceFiles.Select(file => new CachedSourceFile(file.FullPath, file.RelativePath, file.Extension, ProjectCacheService.Metadata(file.FullPath))).ToList(),
            [new SourceMember(source, @"Pages\Index.cshtml.cs", SourceMemberKind.Method, "IndexModel", "BrokenCachedName", "BrokenCachedName", "BrokenCachedName()", 1, 1)]);
        await cache.SaveAsync(oldEntry);

        var analysis = await new ProjectAnalyzer(resolver, new RoslynSourceMemberParser(), cache).AnalyzeAsync(project);

        Assert.AreEqual(ProjectCacheStatus.Created, analysis.CacheStatus);
        Assert.IsTrue(analysis.Members.Any(member => member.Name == "OnGet"));
        Assert.IsFalse(analysis.Members.Any(member => member.Name == "BrokenCachedName"));
    }
}
