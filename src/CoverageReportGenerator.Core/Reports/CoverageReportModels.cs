using CoverageReportGenerator.Core.Projects;

namespace CoverageReportGenerator.Core.Reports;

/// <summary>
/// HTMLに表示する行カバレッジ状態。
/// </summary>
public enum LineCoverageStatus
{
    /// <summary>dotCoverのStatementが存在しない行。</summary>
    NoData,
    /// <summary>実行済みStatementを含む行。</summary>
    Covered,
    /// <summary>未実行Statementのみを含む行。</summary>
    Uncovered
}

/// <summary>
/// カバレッジツリーのノード種別。
/// </summary>
public enum CoverageTreeKind
{
    /// <summary>プロジェクト。</summary>
    Project,
    /// <summary>アセンブリ。</summary>
    Assembly,
    /// <summary>名前空間。</summary>
    Namespace,
    /// <summary>型。</summary>
    Type,
    /// <summary>メソッド。</summary>
    Method
}

/// <summary>
/// カバレッジレポートを構築するための入力。
/// </summary>
public sealed record CoverageReportRequest(
    ProjectAnalysis Project,
    DotCover.DotCoverReport DotCover,
    CoverageSelection Selection,
    string ReportTitle,
    DateTimeOffset GeneratedAt);

/// <summary>
/// HTMLレンダリングに渡す完成済みレポートモデル。
/// </summary>
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

/// <summary>
/// CoveredStatementsとTotalStatementsから算出する集計。
/// </summary>
public sealed record CoverageSummary(
    int CoveredStatements,
    int TotalStatements,
    decimal? CoveragePercentOverride = null)
{
    /// <summary>
    /// 未カバーStatement数。
    /// </summary>
    public int UncoveredStatements => TotalStatements - CoveredStatements;

    /// <summary>
    /// 小数1桁のカバレッジ率。
    /// </summary>
    public decimal CoveragePercent => CoveragePercentOverride ?? (TotalStatements == 0 ? 0 : decimal.Round(CoveredStatements * 100m / TotalStatements, 1));
}

/// <summary>
/// ファイル単位のカバレッジ表示情報。
/// </summary>
public sealed record FileCoverageReport(
    int FileId,
    string FullPath,
    string RelativePath,
    CoverageSummary Summary,
    IReadOnlyList<LineCoverageReport> Lines,
    bool SourceFound);

/// <summary>
/// ソース1行分の表示情報。
/// </summary>
public sealed record LineCoverageReport(
    int LineNumber,
    string Text,
    LineCoverageStatus Status);

/// <summary>
/// メンバー単位のカバレッジ表示情報。
/// </summary>
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

/// <summary>
/// AssemblyからMethodまでの階層ツリーノード。
/// </summary>
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
    /// <summary>
    /// 子ノード一覧。
    /// </summary>
    public IReadOnlyList<CoverageTreeItem> Children { get; init; } = [];
}

/// <summary>
/// カバレッジが低い箇所のランキング。
/// </summary>
public sealed record CoverageRankings(
    IReadOnlyList<CoverageTreeItem> LowestNamespaces,
    IReadOnlyList<CoverageTreeItem> LowestTypes,
    IReadOnlyList<MemberCoverageReport> LowestMembers,
    IReadOnlyList<FileCoverageReport> MostUncoveredFiles);

/// <summary>
/// カバレッジツリーを扱う拡張メソッド。
/// </summary>
public static class CoverageTreeExtensions
{
    /// <summary>
    /// ツリーを深さ優先で平坦化する。
    /// </summary>
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
