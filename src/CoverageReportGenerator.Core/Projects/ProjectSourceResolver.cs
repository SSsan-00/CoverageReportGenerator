using System.Xml.Linq;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// .csprojから解析対象ソースファイルを解決する。
/// </summary>
public sealed class ProjectSourceResolver
{
    private static readonly string[] SourcePatterns = ["*.cs", "*.cshtml"];
    private static readonly string[] ExcludedSegments = ["bin", "obj", ".git"];

    /// <summary>
    /// プロジェクト配下の.csと.cshtmlを収集する。
    /// </summary>
    public Task<ProjectSourceSnapshot> ResolveAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullProjectPath = PathUtilities.NormalizeFullPath(projectPath);
        if (!Path.GetExtension(fullProjectPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Project analysis requires a .csproj file.", nameof(projectPath));
        }

        if (!File.Exists(fullProjectPath))
        {
            throw new FileNotFoundException("Project file was not found.", fullProjectPath);
        }

        var projectRoot = Path.GetDirectoryName(fullProjectPath)
            ?? throw new InvalidOperationException("Project file must have a directory.");
        var projectName = Path.GetFileNameWithoutExtension(fullProjectPath);
        var sourceFiles = ResolveSourceFiles(fullProjectPath, projectRoot)
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ProjectSourceSnapshot(fullProjectPath, projectName, projectRoot, sourceFiles));
    }

    private static IEnumerable<SourceFile> ResolveSourceFiles(string projectPath, string projectRoot)
    {
        XDocument? document = null;
        try
        {
            document = XDocument.Load(projectPath);
        }
        catch
        {
            // Fall back to SDK-style directory scanning when the project XML cannot be inspected.
        }

        // SDK-styleプロジェクトでは暗黙includeが多いため、明示includeと実ファイル走査を併用する。
        var explicitIncludes = document?
            .Descendants()
            .Where(element => IsSourceItem(element.Name.LocalName))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList() ?? [];

        var paths = SourcePatterns
            .SelectMany(pattern => Directory.EnumerateFiles(projectRoot, pattern, SearchOption.AllDirectories))
            .Concat(ResolveIncludes(projectRoot, explicitIncludes));

        return paths
            .Select(PathUtilities.NormalizeFullPath)
            .Where(File.Exists)
            .Where(path => !IsExcluded(projectRoot, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new SourceFile(path, PathUtilities.GetRelativePath(projectRoot, path), Path.GetExtension(path)));
    }

    private static bool IsSourceItem(string itemName)
    {
        return itemName is "Compile" or "Content" or "None" or "Page";
    }

    private static IEnumerable<string> ResolveIncludes(string projectRoot, IEnumerable<string> includes)
    {
        foreach (var include in includes)
        {
            var normalized = include.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (normalized.Contains('*') || normalized.Contains('?'))
            {
                var directoryPart = Path.GetDirectoryName(normalized);
                var searchRoot = string.IsNullOrEmpty(directoryPart)
                    ? projectRoot
                    : Path.Combine(projectRoot, directoryPart.Replace("**", string.Empty).TrimEnd(Path.DirectorySeparatorChar));
                var pattern = Path.GetFileName(normalized);
                if (Directory.Exists(searchRoot))
                {
                    foreach (var file in Directory.EnumerateFiles(searchRoot, pattern, SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
            }
            else
            {
                yield return Path.Combine(projectRoot, normalized);
            }
        }
    }

    private static bool IsExcluded(string projectRoot, string path)
    {
        var relative = PathUtilities.GetRelativePath(projectRoot, path);
        return ExcludedSegments.Any(segment => PathUtilities.ContainsPathSegment(relative, segment));
    }
}
