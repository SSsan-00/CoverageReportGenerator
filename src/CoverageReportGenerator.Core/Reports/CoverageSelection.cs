namespace CoverageReportGenerator.Core.Reports;

/// <summary>
/// レポート生成対象の範囲種別。
/// </summary>
public enum CoverageScopeType
{
    /// <summary>プロジェクト全体。</summary>
    Project,
    /// <summary>指定フォルダ配下。</summary>
    Folder,
    /// <summary>指定ファイルのみ。</summary>
    File
}

/// <summary>
/// UIで指定された解析対象範囲とファイルフィルタ。
/// </summary>
public sealed record CoverageSelection(
    CoverageScopeType ScopeType,
    string? ScopePath,
    string IncludePatterns,
    string ExcludePatterns)
{
    /// <summary>
    /// プロジェクト全体を対象にする既定条件を作る。
    /// </summary>
    public static CoverageSelection Project(string includePatterns = "*.cs;*.cshtml", string excludePatterns = "*.g.cs;*.generated.cs;*.Designer.cs;bin;obj")
    {
        return new CoverageSelection(CoverageScopeType.Project, null, includePatterns, excludePatterns);
    }
}
