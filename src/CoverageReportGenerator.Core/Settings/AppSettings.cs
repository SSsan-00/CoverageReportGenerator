using CoverageReportGenerator.Core.Reports;

namespace CoverageReportGenerator.Core.Settings;

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
