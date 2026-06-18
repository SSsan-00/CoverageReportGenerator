using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Tests.TestSupport;

namespace CoverageReportGenerator.Core.Tests;

public sealed class ProjectAnalysisTests
{
    [Fact]
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

        Assert.Equal(3, snapshot.SourceFiles.Count);
        Assert.Equal(2, selected.IncludedFiles.Count);
        Assert.All(selected.IncludedFiles, file => Assert.Contains(@"\Pages\Admin\", file.FullPath));
        Assert.DoesNotContain(selected.IncludedFiles, file => file.FullPath.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase));
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

        Assert.Equal(ProjectCacheStatus.Created, first.CacheStatus);
        Assert.Equal(ProjectCacheStatus.Valid, second.CacheStatus);

        await File.AppendAllTextAsync(source, Environment.NewLine + "public class Extra { public void Run() {} }");
        var third = await analyzer.AnalyzeAsync(project);

        Assert.Equal(ProjectCacheStatus.Updated, third.CacheStatus);
        Assert.Contains(third.Members, member => member.Name == "Run");
    }
}
