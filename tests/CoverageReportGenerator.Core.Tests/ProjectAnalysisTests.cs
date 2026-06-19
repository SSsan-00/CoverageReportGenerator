using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Tests.TestSupport;
using System.Text;

namespace CoverageReportGenerator.Core.Tests;

public sealed class ProjectAnalysisTests
{
    [Fact]
    public async Task Source_resolver_rejects_paths_that_are_not_csproj_files()
    {
        using var workspace = TestWorkspace.Create();
        var solution = workspace.Write("Sample.sln", string.Empty);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new ProjectSourceResolver().ResolveAsync(solution));

        Assert.Contains(".csproj", ex.Message);
    }

    [Fact]
    public async Task Source_resolver_reads_razor_project_files_and_applies_folder_scope_recursively()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
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

        Assert.Equal(3, snapshot.SourceFiles.Count);
        Assert.Equal(2, selected.IncludedFiles.Count);
        Assert.All(selected.IncludedFiles, file => Assert.Contains(@"\Pages\Admin\", file.FullPath));
        Assert.DoesNotContain(selected.IncludedFiles, file => file.FullPath.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Source_resolver_does_not_stop_directory_scanning_when_compile_include_exists()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <Compile Include="Linked\OnlyExplicit.cs" />
              </ItemGroup>
            </Project>
            """);
        workspace.Write(@"Linked\OnlyExplicit.cs", "public class OnlyExplicit { }");
        workspace.Write(@"Pages\Index.cshtml", "@page");
        workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");

        var snapshot = await new ProjectSourceResolver().ResolveAsync(project);

        Assert.Contains(snapshot.SourceFiles, file => file.RelativePath == @"Linked\OnlyExplicit.cs");
        Assert.Contains(snapshot.SourceFiles, file => file.RelativePath == @"Pages\Index.cshtml");
        Assert.Contains(snapshot.SourceFiles, file => file.RelativePath == @"Pages\Index.cshtml.cs");
    }

    [Fact]
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

        Assert.Contains(members, member => member.Kind == SourceMemberKind.Property && member.Name == "Name");
        Assert.Contains(members, member => member.Kind == SourceMemberKind.Method && member.Name == "OnPostAsync");
        Assert.Contains(members, member => member.Kind == SourceMemberKind.LocalFunction && member.Name == "Validate");
        Assert.Contains(members, member => member.Kind == SourceMemberKind.Lambda && member.DisplayName.Contains("lambda in OnPostAsync"));
    }

    [Fact]
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

        Assert.Contains(members, member => member.Kind == SourceMemberKind.Method && member.Name == "日本語メソッド");
    }

    [Fact]
    public async Task Project_cache_reuses_unchanged_member_analysis_and_rebuilds_changed_files()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var source = workspace.Write(@"Pages\Index.cshtml.cs", "public class IndexModel { public void OnGet() {} }");
        var cacheDir = workspace.CreateDirectory("cache");

        var analyzer = new ProjectAnalyzer(new ProjectSourceResolver(), new RoslynSourceMemberParser(), new ProjectCacheService(cacheDir));
        var first = await analyzer.AnalyzeAsync(project);
        var second = await analyzer.AnalyzeAsync(project);

        Assert.Equal(ProjectCacheStatus.Created, first.CacheStatus);
        Assert.Equal(ProjectCacheStatus.Valid, second.CacheStatus);

        await File.AppendAllTextAsync(source, Environment.NewLine + "public class Extra { public void Run() {} }");
        var third = await analyzer.AnalyzeAsync(project);

        Assert.Equal(ProjectCacheStatus.Updated, third.CacheStatus);
        Assert.Contains(third.Members, member => member.Name == "Run");
    }

    [Fact]
    public async Task Project_cache_ignores_entries_from_previous_schema_versions()
    {
        using var workspace = TestWorkspace.Create();
        var project = workspace.Write("Sample.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup>
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

        Assert.Equal(ProjectCacheStatus.Created, analysis.CacheStatus);
        Assert.Contains(analysis.Members, member => member.Name == "OnGet");
        Assert.DoesNotContain(analysis.Members, member => member.Name == "BrokenCachedName");
    }
}
