using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Reports;

public sealed class CoverageTargetSelector
{
    private readonly FilePatternMatcher _matcher = new();

    public CoverageTargetSelection Select(ProjectSourceSnapshot snapshot, CoverageSelection selection)
    {
        var included = snapshot.SourceFiles
            .Where(file => IsInScope(snapshot.ProjectRoot, file.FullPath, selection))
            .Where(file => _matcher.IsIncluded(file.RelativePath, selection.IncludePatterns, selection.ExcludePatterns))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CoverageTargetSelection(selection, included);
    }

    private static bool IsInScope(string projectRoot, string filePath, CoverageSelection selection)
    {
        return selection.ScopeType switch
        {
            CoverageScopeType.Project => true,
            CoverageScopeType.Folder => !string.IsNullOrWhiteSpace(selection.ScopePath)
                && PathUtilities.IsDescendantOrSame(selection.ScopePath, filePath),
            CoverageScopeType.File => !string.IsNullOrWhiteSpace(selection.ScopePath)
                && PathUtilities.NormalizeFullPath(selection.ScopePath).Equals(PathUtilities.NormalizeFullPath(filePath), StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}

public sealed record CoverageTargetSelection(
    CoverageSelection Selection,
    IReadOnlyList<SourceFile> IncludedFiles);
