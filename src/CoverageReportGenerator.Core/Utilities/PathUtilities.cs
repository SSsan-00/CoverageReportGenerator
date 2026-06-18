namespace CoverageReportGenerator.Core.Utilities;

internal static class PathUtilities
{
    public static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    }

    public static string GetRelativePath(string root, string path)
    {
        return NormalizeRelativePath(Path.GetRelativePath(root, path));
    }

    public static bool IsDescendantOrSame(string root, string candidate)
    {
        var normalizedRoot = NormalizeFullPath(root) + Path.DirectorySeparatorChar;
        var normalizedCandidate = NormalizeFullPath(candidate);
        return normalizedCandidate.Equals(NormalizeFullPath(root), StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsPathSegment(string path, string segment)
    {
        var parts = NormalizeRelativePath(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }
}
