using CoverageReportGenerator.Core.Projects;

namespace CoverageReportGenerator.Core.Reports;

public enum LineCoverageStatus
{
    NoData,
    Covered,
    Uncovered
}

public enum CoverageTreeKind
{
    Project,
    Assembly,
    Namespace,
    Type,
    Method
}

public sealed record CoverageReportRequest(
    ProjectAnalysis Project,
    DotCover.DotCoverReport DotCover,
    CoverageSelection Selection,
    string ReportTitle,
    DateTimeOffset GeneratedAt);

public sealed record CoverageReport(
    string ReportTitle,
    string ProjectName,
    string ProjectPath,
    string ScopeLabel,
    DateTimeOffset GeneratedAt,
    CoverageSummary Summary,
    IReadOnlyList<FileCoverageReport> Files,
    IReadOnlyList<MemberCoverageReport> Members,
    IReadOnlyList<CoverageTreeItem> Tree,
    CoverageRankings Rankings);

public sealed record CoverageSummary(
    int CoveredStatements,
    int TotalStatements)
{
    public int UncoveredStatements => TotalStatements - CoveredStatements;
    public decimal CoveragePercent => TotalStatements == 0 ? 0 : decimal.Round(CoveredStatements * 100m / TotalStatements, 1);
}

public sealed record FileCoverageReport(
    int FileId,
    string FullPath,
    string RelativePath,
    CoverageSummary Summary,
    IReadOnlyList<LineCoverageReport> Lines,
    bool SourceFound);

public sealed record LineCoverageReport(
    int LineNumber,
    string Text,
    LineCoverageStatus Status);

public sealed record MemberCoverageReport(
    int FileId,
    string FilePath,
    string RelativePath,
    SourceMemberKind Kind,
    string ContainingType,
    string Name,
    string DisplayName,
    string DisplaySignature,
    int StartLine,
    int EndLine,
    CoverageSummary Summary,
    IReadOnlyList<string> RawDotCoverMethodNames);

public sealed record CoverageTreeItem(
    string Id,
    string? ParentId,
    CoverageTreeKind Kind,
    string Name,
    int Depth,
    CoverageSummary Summary,
    int? FileId,
    int? StartLine)
{
    public IReadOnlyList<CoverageTreeItem> Children { get; init; } = [];
}

public sealed record CoverageRankings(
    IReadOnlyList<CoverageTreeItem> LowestNamespaces,
    IReadOnlyList<CoverageTreeItem> LowestTypes,
    IReadOnlyList<MemberCoverageReport> LowestMembers,
    IReadOnlyList<FileCoverageReport> MostUncoveredFiles);

public static class CoverageTreeExtensions
{
    public static IEnumerable<CoverageTreeItem> Flatten(this IEnumerable<CoverageTreeItem> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in root.Children.Flatten())
            {
                yield return child;
            }
        }
    }
}
