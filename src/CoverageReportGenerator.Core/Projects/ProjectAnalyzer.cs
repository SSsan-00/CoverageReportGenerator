using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

public sealed class ProjectAnalyzer
{
    private readonly ProjectSourceResolver _resolver;
    private readonly RoslynSourceMemberParser _memberParser;
    private readonly ProjectCacheService _cache;

    public ProjectAnalyzer()
        : this(new ProjectSourceResolver(), new RoslynSourceMemberParser(), new ProjectCacheService())
    {
    }

    public ProjectAnalyzer(ProjectSourceResolver resolver, RoslynSourceMemberParser memberParser, ProjectCacheService cache)
    {
        _resolver = resolver;
        _memberParser = memberParser;
        _cache = cache;
    }

    public async Task<ProjectAnalysis> AnalyzeAsync(string projectPath, IProgress<ProjectAnalysisProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var snapshot = await _resolver.ResolveAsync(projectPath, cancellationToken);
        var cached = await _cache.LoadAsync(snapshot, cancellationToken);
        if (cached is not null && _cache.IsProjectMetadataValid(snapshot, cached))
        {
            return new ProjectAnalysis(snapshot.ProjectPath, snapshot.ProjectName, snapshot.ProjectRoot, snapshot.SourceFiles, cached.Members, ProjectCacheStatus.Valid);
        }

        var changedMembers = new List<SourceMember>();
        var reusableMembers = new List<SourceMember>();
        var cachedFileMap = cached?.SourceFiles.ToDictionary(file => file.FullPath, PathUtilities.PathComparer) ?? [];

        foreach (var file in snapshot.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canReuse = cached is not null
                && cachedFileMap.TryGetValue(file.FullPath, out var cachedFile)
                && ProjectCacheService.SameMetadata(ProjectCacheService.Metadata(file.FullPath), cachedFile.Metadata);

            if (canReuse)
            {
                reusableMembers.AddRange(cached!.Members.Where(member => PathUtilities.PathComparer.Equals(member.FilePath, file.FullPath)));
                continue;
            }

            progress?.Report(new ProjectAnalysisProgress($"Analyzing {file.RelativePath}"));
            if (file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                changedMembers.AddRange(await _memberParser.ParseFileAsync(file.FullPath, cancellationToken));
            }
        }

        var members = reusableMembers
            .Concat(changedMembers)
            .OrderBy(member => member.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.StartLine)
            .ToList();

        var status = cached is null ? ProjectCacheStatus.Created : ProjectCacheStatus.Updated;
        var analysis = new ProjectAnalysis(snapshot.ProjectPath, snapshot.ProjectName, snapshot.ProjectRoot, snapshot.SourceFiles, members, status);
        await _cache.SaveAsync(_cache.CreateEntry(analysis), cancellationToken);
        return analysis;
    }
}

public sealed record ProjectAnalysisProgress(string Message);
