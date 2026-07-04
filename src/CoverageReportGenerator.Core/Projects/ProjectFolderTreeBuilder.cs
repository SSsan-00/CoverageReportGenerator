using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// プロジェクト内フォルダをツリー表示するための情報。
/// </summary>
public sealed record ProjectFolderNode(
    string Name,
    string RelativePath,
    string FullPath,
    int SourceFileCount,
    int MemberCount,
    IReadOnlyList<ProjectFolderNode> Children);

/// <summary>
/// 解析済みプロジェクトから対象選択用のフォルダツリーを組み立てる。
/// </summary>
public sealed class ProjectFolderTreeBuilder
{
    /// <summary>
    /// ソースファイルとメンバー数をフォルダごとに集計したツリーを返す。
    /// </summary>
    public ProjectFolderNode Build(ProjectAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var folders = new Dictionary<string, FolderAccumulator>(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = new FolderAccumulator()
        };

        foreach (var sourceFile in analysis.SourceFiles)
        {
            var folderPath = RelativeFolderOf(sourceFile.RelativePath);
            foreach (var ancestor in AncestorsOf(folderPath))
            {
                GetOrCreate(folders, ancestor).SourceFileCount++;
            }
        }

        foreach (var member in analysis.Members)
        {
            var folderPath = RelativeFolderOf(member.RelativePath);
            foreach (var ancestor in AncestorsOf(folderPath))
            {
                GetOrCreate(folders, ancestor).MemberCount++;
            }
        }

        return BuildNode(analysis, folders, string.Empty);
    }

    private static FolderAccumulator GetOrCreate(
        IDictionary<string, FolderAccumulator> folders,
        string relativePath)
    {
        if (!folders.TryGetValue(relativePath, out var accumulator))
        {
            accumulator = new FolderAccumulator();
            folders.Add(relativePath, accumulator);
        }

        return accumulator;
    }

    private static ProjectFolderNode BuildNode(
        ProjectAnalysis analysis,
        IReadOnlyDictionary<string, FolderAccumulator> folders,
        string relativePath)
    {
        var accumulator = folders[relativePath];
        var children = folders.Keys
            .Where(path => !string.IsNullOrEmpty(path) && ParentOf(path).Equals(relativePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => NameOf(analysis, path), StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildNode(analysis, folders, path))
            .ToList();

        return new ProjectFolderNode(
            NameOf(analysis, relativePath),
            relativePath,
            FullPathOf(analysis.ProjectRoot, relativePath),
            accumulator.SourceFileCount,
            accumulator.MemberCount,
            children);
    }

    private static IEnumerable<string> AncestorsOf(string relativeFolder)
    {
        yield return string.Empty;

        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            yield break;
        }

        var current = string.Empty;
        foreach (var part in relativeFolder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = string.IsNullOrEmpty(current) ? part : Path.Combine(current, part);
            yield return current;
        }
    }

    private static string RelativeFolderOf(string relativeFilePath)
    {
        var normalized = PathUtilities.NormalizeRelativePath(relativeFilePath);
        return PathUtilities.NormalizeRelativePath(Path.GetDirectoryName(normalized) ?? string.Empty);
    }

    private static string ParentOf(string relativePath)
    {
        return PathUtilities.NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
    }

    private static string NameOf(ProjectAnalysis analysis, string relativePath)
    {
        return string.IsNullOrEmpty(relativePath) ? analysis.ProjectName : Path.GetFileName(relativePath);
    }

    private static string FullPathOf(string projectRoot, string relativePath)
    {
        return string.IsNullOrEmpty(relativePath) ? projectRoot : Path.Combine(projectRoot, relativePath);
    }

    private sealed class FolderAccumulator
    {
        public int SourceFileCount { get; set; }

        public int MemberCount { get; set; }
    }
}
