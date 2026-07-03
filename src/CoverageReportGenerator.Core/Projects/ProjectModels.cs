namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// Roslynで検出したソース上のメンバー種別。
/// </summary>
public enum SourceMemberKind
{
    /// <summary>通常メソッド。</summary>
    Method,
    /// <summary>コンストラクタ。</summary>
    Constructor,
    /// <summary>プロパティ。</summary>
    Property,
    /// <summary>プロパティやインデクサのアクセサ。</summary>
    Accessor,
    /// <summary>ローカル関数。</summary>
    LocalFunction,
    /// <summary>ラムダ式。</summary>
    Lambda
}

/// <summary>
/// プロジェクト解析キャッシュの利用結果。
/// </summary>
public enum ProjectCacheStatus
{
    /// <summary>キャッシュ未使用。</summary>
    Disabled,
    /// <summary>キャッシュを新規作成。</summary>
    Created,
    /// <summary>既存キャッシュを利用。</summary>
    Valid,
    /// <summary>一部変更によりキャッシュを更新。</summary>
    Updated,
    /// <summary>既存キャッシュが利用不可。</summary>
    Invalid
}

/// <summary>
/// 解析対象として検出したソースファイル。
/// </summary>
public sealed record SourceFile(
    string FullPath,
    string RelativePath,
    string Extension);

/// <summary>
/// .csprojから解決したプロジェクト内ソース一覧。
/// </summary>
public sealed record ProjectSourceSnapshot(
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    IReadOnlyList<SourceFile> SourceFiles);

/// <summary>
/// ソース上で検出したメソッドやプロパティの位置情報。
/// </summary>
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

/// <summary>
/// プロジェクト解析の結果。
/// </summary>
public sealed record ProjectAnalysis(
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    IReadOnlyList<SourceFile> SourceFiles,
    IReadOnlyList<SourceMember> Members,
    ProjectCacheStatus CacheStatus);
