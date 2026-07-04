namespace CoverageReportGenerator.Core.DotCover;

/// <summary>
/// dotCover DetailedXMLから抽出したカバレッジ情報。
/// </summary>
public sealed record DotCoverReport(
    CoverageMetric Root,
    IReadOnlyList<DotCoverFileIndex> Files,
    IReadOnlyList<DotCoverStatement> Statements);

/// <summary>
/// CoveredStatementsとTotalStatementsを中心にした集計値。
/// </summary>
public sealed record CoverageMetric(
    int CoveredStatements,
    int TotalStatements,
    decimal? CoveragePercent);

/// <summary>
/// dotCover XML内のFileIndices/File要素。
/// </summary>
public sealed record DotCoverFileIndex(
    string Index,
    string Name);

/// <summary>
/// dotCover XML内のStatement要素、ソース範囲、所属階層。
/// </summary>
public sealed record DotCoverStatement(
    string FileIndex,
    int Line,
    bool Covered,
    string AssemblyName,
    string NamespaceName,
    string TypeName,
    string MethodName,
    int? Column = null,
    int? EndLine = null,
    int? EndColumn = null,
    string? MethodKey = null,
    CoverageMetric? MethodMetric = null);

/// <summary>
/// dotCover XMLを解析できない場合の例外。
/// </summary>
public sealed class DotCoverParseException : Exception
{
    /// <summary>
    /// メッセージを指定して例外を生成する。
    /// </summary>
    public DotCoverParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// メッセージと内部例外を指定して例外を生成する。
    /// </summary>
    public DotCoverParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
