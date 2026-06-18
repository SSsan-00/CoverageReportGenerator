namespace CoverageReportGenerator.Core.DotCover;

public sealed record DotCoverReport(
    CoverageMetric Root,
    IReadOnlyList<DotCoverFileIndex> Files,
    IReadOnlyList<DotCoverStatement> Statements);

public sealed record CoverageMetric(
    int CoveredStatements,
    int TotalStatements,
    decimal? CoveragePercent);

public sealed record DotCoverFileIndex(
    string Index,
    string Name);

public sealed record DotCoverStatement(
    string FileIndex,
    int Line,
    bool Covered,
    string AssemblyName,
    string NamespaceName,
    string TypeName,
    string MethodName);

public sealed class DotCoverParseException : Exception
{
    public DotCoverParseException(string message)
        : base(message)
    {
    }

    public DotCoverParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
