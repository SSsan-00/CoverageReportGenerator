namespace CoverageReportGenerator.Core.Reports;

public enum CoverageScopeType
{
    Project,
    Folder,
    File
}

public sealed record CoverageSelection(
    CoverageScopeType ScopeType,
    string? ScopePath,
    string IncludePatterns,
    string ExcludePatterns)
{
    public static CoverageSelection Project(string includePatterns = "*.cs;*.cshtml", string excludePatterns = "*.g.cs;*.generated.cs;*.Designer.cs;bin;obj")
    {
        return new CoverageSelection(CoverageScopeType.Project, null, includePatterns, excludePatterns);
    }
}
