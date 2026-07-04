using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// .csprojを起点にソースファイルとメンバー情報を解析する。
/// </summary>
public sealed class ProjectAnalyzer
{
    private readonly ProjectSourceResolver _resolver;
    private readonly RoslynSourceMemberParser _memberParser;
    private readonly ProjectCacheService _cache;

    /// <summary>
    /// 既定の依存関係で解析器を生成する。
    /// </summary>
    public ProjectAnalyzer()
        : this(new ProjectSourceResolver(), new RoslynSourceMemberParser(), new ProjectCacheService())
    {
    }

    /// <summary>
    /// 依存関係を指定して解析器を生成する。
    /// </summary>
    public ProjectAnalyzer(ProjectSourceResolver resolver, RoslynSourceMemberParser memberParser, ProjectCacheService cache)
    {
        _resolver = resolver;
        _memberParser = memberParser;
        _cache = cache;
    }

    /// <summary>
    /// プロジェクトを解析し、変更のないファイルはキャッシュ済みメンバーを再利用する。
    /// </summary>
    public async Task<ProjectAnalysis> AnalyzeAsync(string projectPath, IProgress<ProjectAnalysisProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var snapshot = await _resolver.ResolveAsync(projectPath, cancellationToken);
        var cached = await _cache.LoadAsync(snapshot, cancellationToken);
        if (cached is not null && _cache.IsProjectMetadataValid(snapshot, cached))
        {
            var cachedMembers = NormalizeMemberRelativePaths(snapshot, cached.Members);
            return new ProjectAnalysis(snapshot.ProjectPath, snapshot.ProjectName, snapshot.ProjectRoot, snapshot.SourceFiles, cachedMembers, ProjectCacheStatus.Valid);
        }

        var changedMembers = new List<SourceMember>();
        var reusableMembers = new List<SourceMember>();
        var cachedFileMap = cached?.SourceFiles.ToDictionary(file => file.FullPath, PathUtilities.PathComparer) ?? [];

        // ファイル単位の更新日時とサイズで、Roslyn解析を再実行する範囲を絞る。
        foreach (var file in snapshot.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canReuse = cached is not null
                && cachedFileMap.TryGetValue(file.FullPath, out var cachedFile)
                && ProjectCacheService.SameMetadata(ProjectCacheService.Metadata(file.FullPath), cachedFile.Metadata);

            if (canReuse)
            {
                reusableMembers.AddRange(cached!.Members
                    .Where(member => PathUtilities.PathComparer.Equals(member.FilePath, file.FullPath))
                    .Select(member => member with { RelativePath = file.RelativePath }));
                continue;
            }

            progress?.Report(new ProjectAnalysisProgress($"解析中: {file.RelativePath}"));
            if (file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var parsedMembers = await _memberParser.ParseFileAsync(file.FullPath, cancellationToken);
                changedMembers.AddRange(parsedMembers.Select(member => member with { RelativePath = file.RelativePath }));
            }
        }

        var members = NormalizeMemberRelativePaths(snapshot, reusableMembers.Concat(changedMembers));

        var status = cached is null ? ProjectCacheStatus.Created : ProjectCacheStatus.Updated;
        var analysis = new ProjectAnalysis(snapshot.ProjectPath, snapshot.ProjectName, snapshot.ProjectRoot, snapshot.SourceFiles, members, status);
        await _cache.SaveAsync(_cache.CreateEntry(analysis), cancellationToken);
        return analysis;
    }

    private static IReadOnlyList<SourceMember> NormalizeMemberRelativePaths(ProjectSourceSnapshot snapshot, IEnumerable<SourceMember> members)
    {
        var sourceFiles = snapshot.SourceFiles.ToDictionary(file => file.FullPath, PathUtilities.PathComparer);
        return members
            .Select(member => sourceFiles.TryGetValue(member.FilePath, out var file)
                ? member with { RelativePath = file.RelativePath }
                : member)
            .OrderBy(member => member.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.StartLine)
            .ToList();
    }
}

/// <summary>
/// プロジェクト解析中の進捗メッセージ。
/// </summary>
public sealed record ProjectAnalysisProgress(string Message);
