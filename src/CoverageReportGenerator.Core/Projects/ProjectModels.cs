namespace CoverageReportGenerator.Core.Projects;

public enum SourceMemberKind
{
    Method,
    Constructor,
    Property,
    Accessor,
    LocalFunction,
    Lambda
}

public enum ProjectCacheStatus
{
    Disabled,
    Created,
    Valid,
    Updated,
    Invalid
}

public sealed record SourceFile(
    string FullPath,
    string RelativePath,
    string Extension);

public sealed record ProjectSourceSnapshot(
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    IReadOnlyList<SourceFile> SourceFiles);

public sealed record SourceMember(
    string FilePath,
    string RelativePath,
    SourceMemberKind Kind,
    string ContainingType,
    string Name,
    string DisplayName,
    string DisplaySignature,
    int StartLine,
    int EndLine);

public sealed record ProjectAnalysis(
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    IReadOnlyList<SourceFile> SourceFiles,
    IReadOnlyList<SourceMember> Members,
    ProjectCacheStatus CacheStatus);
