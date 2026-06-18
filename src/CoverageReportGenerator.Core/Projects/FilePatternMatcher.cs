using System.Text.RegularExpressions;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

public sealed class FilePatternMatcher
{
    public bool IsIncluded(string relativePath, string includePatterns, string excludePatterns)
    {
        var normalized = PathUtilities.NormalizeRelativePath(relativePath);
        var fileName = Path.GetFileName(normalized);

        var includes = SplitPatterns(includePatterns).DefaultIfEmpty("*").ToArray();
        var excludes = SplitPatterns(excludePatterns).ToArray();

        var included = includes.Any(pattern => Matches(pattern, fileName, normalized));
        if (!included)
        {
            return false;
        }

        return !excludes.Any(pattern => Matches(pattern, fileName, normalized));
    }

    private static IEnumerable<string> SplitPatterns(string? patterns)
    {
        return (patterns ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern));
    }

    private static bool Matches(string pattern, string fileName, string relativePath)
    {
        var normalizedPattern = pattern.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return false;
        }

        if (!normalizedPattern.Contains('*') && !normalizedPattern.Contains('?') && !normalizedPattern.Contains(Path.DirectorySeparatorChar))
        {
            if (PathUtilities.ContainsPathSegment(relativePath, normalizedPattern))
            {
                return true;
            }
        }

        return Wildcard(normalizedPattern).IsMatch(fileName) || Wildcard(normalizedPattern).IsMatch(relativePath);
    }

    private static Regex Wildcard(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
