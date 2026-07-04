using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Settings;
using CoverageReportGenerator.Core.Tests.TestSupport;

namespace CoverageReportGenerator.Core.Tests;

/// <summary>
/// アプリ設定保存サービスのテスト。
/// </summary>
[TestClass]
public sealed class AppSettingsServiceTests
{
    /// <summary>
    /// 設定ファイルがない場合に既定値を返すことを検証する。
    /// </summary>
    [TestMethod]
    public void Load_returns_defaults_when_settings_file_does_not_exist()
    {
        using var workspace = TestWorkspace.Create();
        var service = new AppSettingsService(workspace.PathOf("settings.json"));

        var settings = service.Load();

        Assert.AreEqual(AppSettings.Defaults, settings);
    }

    /// <summary>
    /// 保存した入力値を再読込できることを検証する。
    /// </summary>
    [TestMethod]
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

        Assert.AreEqual(saved, loaded);
    }

    /// <summary>
    /// Resetで保存済み設定を削除することを検証する。
    /// </summary>
    [TestMethod]
    public void Reset_removes_saved_settings()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.PathOf("settings.json");
        var service = new AppSettingsService(path);
        service.Save(AppSettings.Defaults with { ProjectPath = "Sample.Web.csproj" });

        service.Reset();

        Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(AppSettings.Defaults, service.Load());
    }

    /// <summary>
    /// 不正な設定ファイルを既定値へ戻すことを検証する。
    /// </summary>
    [TestMethod]
    public void Load_returns_defaults_when_settings_file_is_invalid()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.Write("settings.json", "{ invalid json");
        var service = new AppSettingsService(path);

        var settings = service.Load();

        Assert.AreEqual(AppSettings.Defaults, settings);
    }

    /// <summary>
    /// 読込可能だが欠損や不正値を含む設定を既定値で補正することを検証する。
    /// </summary>
    [TestMethod]
    public void Load_normalizes_missing_or_invalid_saved_values()
    {
        using var workspace = TestWorkspace.Create();
        var path = workspace.Write("settings.json", """
            {
              "ProjectPath": null,
              "DotCoverXmlPath": "coverage.xml",
              "ScopePath": null,
              "IncludePatterns": " ",
              "ExcludePatterns": null,
              "OutputDirectory": "reports",
              "ReportTitle": null,
              "ScopeType": 999,
              "OpenAfterGeneration": false,
              "OverwriteExisting": true
            }
            """);
        var service = new AppSettingsService(path);

        var settings = service.Load();

        Assert.AreEqual(AppSettings.Defaults.ProjectPath, settings.ProjectPath);
        Assert.AreEqual("coverage.xml", settings.DotCoverXmlPath);
        Assert.AreEqual(AppSettings.Defaults.ScopePath, settings.ScopePath);
        Assert.AreEqual(AppSettings.Defaults.IncludePatterns, settings.IncludePatterns);
        Assert.AreEqual(AppSettings.Defaults.ExcludePatterns, settings.ExcludePatterns);
        Assert.AreEqual("reports", settings.OutputDirectory);
        Assert.AreEqual(AppSettings.Defaults.ReportTitle, settings.ReportTitle);
        Assert.AreEqual(AppSettings.Defaults.ScopeType, settings.ScopeType);
        Assert.IsFalse(settings.OpenAfterGeneration);
        Assert.IsTrue(settings.OverwriteExisting);
    }
}
