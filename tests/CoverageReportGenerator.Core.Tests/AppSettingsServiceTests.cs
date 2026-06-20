using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Settings;
using CoverageReportGenerator.Core.Tests.TestSupport;

namespace CoverageReportGenerator.Core.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void Load_returns_defaults_when_settings_file_does_not_exist()
    {
        using var workspace = TestWorkspace.Create();
        var service = new AppSettingsService(workspace.PathOf("settings.json"));

        var settings = service.Load();

        Assert.Equal(AppSettings.Defaults, settings);
    }

    [Fact]
    public void Save_and_load_round_trips_last_input_values()
    {
        using var workspace = TestWorkspace.Create();
        var service = new AppSettingsService(workspace.PathOf("settings.json"));
        var saved = new AppSettings(
            ProjectPath: workspace.PathOf("Sample.Web.csproj"),
            DotCoverXmlPath: workspace.PathOf("coverage.xml"),
            ScopePath: workspace.PathOf("Pages"),
            IncludePatterns: "*.cshtml.cs",
            ExcludePatterns: "*.Designer.cs",
            OutputDirectory: workspace.PathOf("report"),
            ReportTitle: "Sample Coverage",
            ScopeType: CoverageScopeType.Folder,
            OpenAfterGeneration: false,
            OverwriteExisting: true);

        service.Save(saved);
        var loaded = service.Load();

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void Reset_removes_saved_settings()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.PathOf("settings.json");
        var service = new AppSettingsService(path);
        service.Save(AppSettings.Defaults with { ProjectPath = "Sample.Web.csproj" });

        service.Reset();

        Assert.False(File.Exists(path));
        Assert.Equal(AppSettings.Defaults, service.Load());
    }

    [Fact]
    public void Load_returns_defaults_when_settings_file_is_invalid()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.Write("settings.json", "{ invalid json");
        var service = new AppSettingsService(path);

        var settings = service.Load();

        Assert.Equal(AppSettings.Defaults, settings);
    }
}
