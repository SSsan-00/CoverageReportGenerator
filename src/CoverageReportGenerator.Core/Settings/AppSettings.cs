using CoverageReportGenerator.Core.Reports;

namespace CoverageReportGenerator.Core.Settings;

/// <summary>
/// WinForms画面で前回入力した内容。
/// </summary>
public sealed record AppSettings(
    string ProjectPath,
    string DotCoverXmlPath,
    string ScopePath,
    string IncludePatterns,
    string ExcludePatterns,
    string OutputDirectory,
    string ReportTitle,
    CoverageScopeType ScopeType,
    bool OpenAfterGeneration,
    bool OverwriteExisting)
{
    /// <summary>
    /// 初回起動時とReset時に使う既定値。
    /// </summary>
    public static AppSettings Defaults { get; } = new(
        ProjectPath: string.Empty,
        DotCoverXmlPath: string.Empty,
        ScopePath: string.Empty,
        IncludePatterns: "*.cs;*.cshtml",
        ExcludePatterns: "*.g.cs;*.generated.cs;*.Designer.cs;bin;obj",
        OutputDirectory: string.Empty,
        ReportTitle: string.Empty,
        ScopeType: CoverageScopeType.Project,
        OpenAfterGeneration: true,
        OverwriteExisting: false);
}
