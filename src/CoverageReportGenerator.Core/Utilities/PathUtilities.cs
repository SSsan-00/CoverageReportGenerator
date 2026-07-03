namespace CoverageReportGenerator.Core.Utilities;

/// <summary>
/// Windows前提のパス比較と正規化をまとめる。
/// </summary>
internal static class PathUtilities
{
    /// <summary>
    /// パス比較用の大文字小文字を無視する比較器。
    /// </summary>
    public static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 絶対パスへ変換し末尾区切り文字を取り除く。
    /// </summary>
    public static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// 相対パスの区切り文字をWindows形式へ揃える。
    /// </summary>
    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// 指定ルートからの相対パスを取得する。
    /// </summary>
    public static string GetRelativePath(string root, string path)
    {
        return NormalizeRelativePath(Path.GetRelativePath(root, path));
    }

    /// <summary>
    /// candidateがroot自身または配下にあるか判定する。
    /// </summary>
    public static bool IsDescendantOrSame(string root, string candidate)
    {
        var normalizedRoot = NormalizeFullPath(root) + Path.DirectorySeparatorChar;
        var normalizedCandidate = NormalizeFullPath(candidate);
        return normalizedCandidate.Equals(NormalizeFullPath(root), StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// パスに指定セグメントが含まれるか判定する。
    /// </summary>
    public static bool ContainsPathSegment(string path, string segment)
    {
        var parts = NormalizeRelativePath(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }
}
