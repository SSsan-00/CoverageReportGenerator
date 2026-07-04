using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoverageReportGenerator.Core.Reports;

/// <summary>
/// プロジェクト解析結果とdotCover情報からHTML用レポートモデルを構築する。
/// </summary>
public sealed class CoverageReportBuilder
{
    /// <summary>
    /// 指定された入力からレポートモデルを生成する。
    /// </summary>
    public CoverageReport Build(CoverageReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetSelection = new CoverageTargetSelector().Select(new ProjectSourceSnapshot(
            request.Project.ProjectPath,
            request.Project.ProjectName,
            request.Project.ProjectRoot,
            request.Project.SourceFiles), request.Selection);

        var fileLookup = BuildFileLookup(request.Project.ProjectRoot, request.DotCover);
        var selectedPaths = targetSelection.IncludedFiles
            .Select(file => PathUtilities.NormalizeFullPath(file.FullPath))
            .ToHashSet(PathUtilities.PathComparer);

        // dotCoverのFileIndexは相対/絶対パスが混在するため、選択済みソースへ正規化して突き合わせる。
        var statements = request.DotCover.Statements
            .Select(statement => ResolveStatement(statement, fileLookup, request.Project.ProjectRoot, targetSelection.IncludedFiles))
            .Where(statement => statement is not null && selectedPaths.Contains(statement.FullPath))
            .Cast<ResolvedStatement>()
            .ToList();

        var fileIds = targetSelection.IncludedFiles
            .Select((file, index) => (file.FullPath, Id: index + 1))
            .ToDictionary(item => PathUtilities.NormalizeFullPath(item.FullPath), item => item.Id, PathUtilities.PathComparer);

        var memberReports = BuildMemberReports(statements, request.Project.Members, fileIds)
            .OrderBy(member => member.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.StartLine)
            .ToList();

        var files = BuildFileReports(targetSelection.IncludedFiles, statements, fileIds);
        var tree = BuildTree(request.Project.ProjectName, statements, memberReports);
        var summary = BuildOverallSummary(request.DotCover, statements);

        return new CoverageReport(
            string.IsNullOrWhiteSpace(request.ReportTitle) ? $"{request.Project.ProjectName} Coverage Report" : request.ReportTitle,
            request.Project.ProjectName,
            request.Project.ProjectPath,
            ScopeLabel(request.Project.ProjectRoot, request.Selection),
            request.GeneratedAt,
            summary,
            files,
            memberReports,
            tree,
            BuildRankings(tree, memberReports, files));
    }

    private static Dictionary<string, string> BuildFileLookup(string projectRoot, DotCoverReport report)
    {
        return report.Files.ToDictionary(
            file => file.Index,
            file => ResolveFileName(projectRoot, file.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveFileName(string projectRoot, string fileName)
    {
        var normalized = fileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return PathUtilities.NormalizeFullPath(Path.IsPathRooted(normalized)
            ? normalized
            : Path.Combine(projectRoot, normalized));
    }

    private static ResolvedStatement? ResolveStatement(
        DotCoverStatement statement,
        IReadOnlyDictionary<string, string> fileLookup,
        string projectRoot,
        IReadOnlyList<SourceFile> targetFiles)
    {
        if (!fileLookup.TryGetValue(statement.FileIndex, out var fullPath))
        {
            return null;
        }

        var matched = MatchTargetPath(fullPath, projectRoot, targetFiles);
        if (matched is null)
        {
            return null;
        }

        return new ResolvedStatement(
            matched.FullPath,
            matched.RelativePath,
            statement.Line,
            statement.EndLine ?? statement.Line,
            statement.Covered,
            statement.AssemblyName,
            statement.NamespaceName,
            statement.TypeName,
            statement.MethodName,
            statement.MethodKey,
            statement.MethodMetric);
    }

    private static SourceFile? MatchTargetPath(string fullPath, string projectRoot, IReadOnlyList<SourceFile> targetFiles)
    {
        var normalized = PathUtilities.NormalizeFullPath(fullPath);
        var exact = targetFiles.FirstOrDefault(file => PathUtilities.PathComparer.Equals(file.FullPath, normalized));
        if (exact is not null)
        {
            return exact;
        }

        var relative = PathUtilities.GetRelativePath(projectRoot, normalized);
        return targetFiles.FirstOrDefault(file =>
            file.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(file.RelativePath, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<FileCoverageReport> BuildFileReports(
        IReadOnlyList<SourceFile> selectedFiles,
        IReadOnlyList<ResolvedStatement> statements,
        IReadOnlyDictionary<string, int> fileIds)
    {
        var groupedStatements = statements
            .GroupBy(statement => statement.FullPath, PathUtilities.PathComparer)
            .ToDictionary(group => group.Key, group => group.ToList(), PathUtilities.PathComparer);

        var reports = new List<FileCoverageReport>();
        foreach (var file in selectedFiles)
        {
            groupedStatements.TryGetValue(file.FullPath, out var fileStatements);
            fileStatements ??= [];
            var lines = ReadLines(file.FullPath, fileStatements);
            reports.Add(new FileCoverageReport(
                fileIds[PathUtilities.NormalizeFullPath(file.FullPath)],
                file.FullPath,
                file.RelativePath,
                SummaryFromDistinctMethods(fileStatements),
                lines,
                File.Exists(file.FullPath)));
        }

        return reports;
    }

    private static IReadOnlyList<LineCoverageReport> ReadLines(string fullPath, IReadOnlyList<ResolvedStatement> statements)
    {
        if (!File.Exists(fullPath))
        {
            return [];
        }

        var statementByLine = BuildStatementsByLine(FilterSourceDisplayStatements(fullPath, statements));
        var sourceLines = SourceTextReader.ReadAllLines(fullPath);
        var lines = new List<LineCoverageReport>(sourceLines.Length);
        for (var i = 0; i < sourceLines.Length; i++)
        {
            var lineNumber = i + 1;
            var status = LineCoverageStatus.NoData;
            if (statementByLine.TryGetValue(lineNumber, out var lineStatements))
            {
                // dotCover公式HTMLは同一行に未カバー範囲が含まれる場合、その行を未カバーとして目視できる。
                status = lineStatements.Any(statement => !statement.Covered)
                    ? LineCoverageStatus.Uncovered
                    : LineCoverageStatus.Covered;
            }

            lines.Add(new LineCoverageReport(lineNumber, sourceLines[i], status));
        }

        return lines;
    }

    private static IReadOnlyList<ResolvedStatement> FilterSourceDisplayStatements(string fullPath, IReadOnlyList<ResolvedStatement> statements)
    {
        var recordPrimaryConstructorRanges = GetRecordPrimaryConstructorRanges(fullPath);
        if (recordPrimaryConstructorRanges.Count == 0)
        {
            return statements;
        }

        return statements
            .Where(statement => !recordPrimaryConstructorRanges.Any(range => statement.Line == range.StartLine && statement.EndLine <= range.EndLine))
            .ToList();
    }

    private static IReadOnlyList<SourceLineRange> GetRecordPrimaryConstructorRanges(string fullPath)
    {
        if (!Path.GetExtension(fullPath).Equals(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return [];
        }

        var text = SourceTextReader.ReadAllText(fullPath);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();
        return root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Where(record => record.ParameterList is not null)
            .Select(record =>
            {
                var identifierSpan = record.SyntaxTree.GetLineSpan(record.Identifier.Span);
                var declarationSpan = record.SyntaxTree.GetLineSpan(record.Span);
                return new SourceLineRange(
                    identifierSpan.StartLinePosition.Line + 1,
                    declarationSpan.EndLinePosition.Line + 1);
            })
            .ToList();
    }

    private static Dictionary<int, List<ResolvedStatement>> BuildStatementsByLine(IReadOnlyList<ResolvedStatement> statements)
    {
        var result = new Dictionary<int, List<ResolvedStatement>>();
        foreach (var statement in statements)
        {
            var startLine = Math.Max(1, statement.Line);
            var endLine = Math.Max(startLine, statement.EndLine);
            for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                if (!result.TryGetValue(lineNumber, out var lineStatements))
                {
                    lineStatements = [];
                    result.Add(lineNumber, lineStatements);
                }

                lineStatements.Add(statement);
            }
        }

        return result;
    }

    private static IReadOnlyList<MemberCoverageReport> BuildMemberReports(
        IReadOnlyList<ResolvedStatement> statements,
        IReadOnlyList<SourceMember> members,
        IReadOnlyDictionary<string, int> fileIds)
    {
        var selectedMemberReports = new List<MemberCoverageReport>();
        var statementsByFile = statements.GroupBy(statement => statement.FullPath, PathUtilities.PathComparer)
            .ToDictionary(group => group.Key, group => group.ToList(), PathUtilities.PathComparer);

        foreach (var member in members)
        {
            var fullPath = PathUtilities.NormalizeFullPath(member.FilePath);
            if (!fileIds.TryGetValue(fullPath, out var fileId))
            {
                continue;
            }

            statementsByFile.TryGetValue(fullPath, out var fileStatements);
            fileStatements ??= [];
            var memberStatements = fileStatements
                .Where(statement => statement.Line <= member.EndLine && statement.EndLine >= member.StartLine)
                .ToList();

            if (memberStatements.Count == 0)
            {
                continue;
            }

            // ASTで得た行範囲に含まれるStatementをメンバー単位の集計として扱う。
            selectedMemberReports.Add(new MemberCoverageReport(
                fileId,
                member.FilePath,
                member.RelativePath,
                member.Kind,
                member.ContainingType,
                member.Name,
                member.DisplayName,
                member.DisplaySignature,
                member.StartLine,
                member.EndLine,
                SummaryFromDistinctMethods(memberStatements),
                memberStatements.Select(statement => statement.MethodName).Distinct(StringComparer.Ordinal).Order().ToList()));
        }

        return selectedMemberReports;
    }

    private static IReadOnlyList<CoverageTreeItem> BuildTree(
        string projectName,
        IReadOnlyList<ResolvedStatement> statements,
        IReadOnlyList<MemberCoverageReport> members)
    {
        var id = 0;
        var roots = new List<CoverageTreeItem>();
        var project = CreateTreeItem(++id, null, CoverageTreeKind.Project, projectName, 0, statements, null, null);
        roots.Add(project);

        var assemblies = statements.GroupBy(statement => statement.AssemblyName).OrderBy(group => group.Key);
        var assemblyChildren = new List<CoverageTreeItem>();
        foreach (var assemblyGroup in assemblies)
        {
            // Assembly > Namespace > Type > Methodの順に、dotCover XMLの階層を保つ。
            var assembly = CreateTreeItem(++id, project.Id, CoverageTreeKind.Assembly, assemblyGroup.Key, 1, assemblyGroup, null, null);
            var namespaceChildren = new List<CoverageTreeItem>();
            foreach (var namespaceGroup in assemblyGroup.GroupBy(statement => statement.NamespaceName).OrderBy(group => group.Key))
            {
                var ns = CreateTreeItem(++id, assembly.Id, CoverageTreeKind.Namespace, namespaceGroup.Key, 2, namespaceGroup, null, null);
                var typeChildren = new List<CoverageTreeItem>();
                foreach (var typeGroup in namespaceGroup.GroupBy(statement => statement.TypeName).OrderBy(group => group.Key))
                {
                    var type = CreateTreeItem(++id, ns.Id, CoverageTreeKind.Type, typeGroup.Key, 3, typeGroup, null, null);
                    var methodChildren = BuildMethodTreeItems(ref id, type.Id, typeGroup, members);
                    typeChildren.Add(type with { Children = methodChildren });
                }

                namespaceChildren.Add(ns with { Children = typeChildren });
            }

            assemblyChildren.Add(assembly with { Children = namespaceChildren });
        }

        roots[0] = project with { Children = assemblyChildren };
        return roots;
    }

    private static IReadOnlyList<CoverageTreeItem> BuildMethodTreeItems(
        ref int id,
        string parentId,
        IEnumerable<ResolvedStatement> statements,
        IReadOnlyList<MemberCoverageReport> members)
    {
        var methodMembers = new List<CoverageTreeItem>();
        var matchedStatements = new HashSet<ResolvedStatement>();
        foreach (var member in members)
        {
            var memberStatements = statements
                .Where(statement => PathUtilities.PathComparer.Equals(statement.FullPath, member.FilePath)
                    && statement.Line <= member.EndLine
                    && statement.EndLine >= member.StartLine)
                .ToList();
            if (memberStatements.Count == 0)
            {
                continue;
            }

            foreach (var statement in memberStatements)
            {
                matchedStatements.Add(statement);
            }

            methodMembers.Add(CreateTreeItem(++id, parentId, CoverageTreeKind.Method, member.DisplayName, 4, memberStatements, member.FileId, member.StartLine));
        }

        foreach (var group in statements.Where(statement => !matchedStatements.Contains(statement)).GroupBy(statement => statement.MethodName).OrderBy(group => group.Key))
        {
            // Roslynで対応できないコンパイラ生成名はdotCover名のままMethodノードへ残す。
            methodMembers.Add(CreateTreeItem(++id, parentId, CoverageTreeKind.Method, group.Key, 4, group, null, null));
        }

        return methodMembers
            .GroupBy(item => (item.Name, item.FileId, item.StartLine))
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CoverageTreeItem CreateTreeItem(
        int number,
        string? parentId,
        CoverageTreeKind kind,
        string name,
        int depth,
        IEnumerable<ResolvedStatement> statements,
        int? fileId,
        int? startLine)
    {
        var materialized = statements.ToList();
        return new CoverageTreeItem(
            $"node-{number}",
            parentId,
            kind,
            name,
            depth,
            SummaryFromDistinctMethods(materialized),
            fileId,
            startLine);
    }

    private static CoverageSummary SummaryFromDistinctMethods(IReadOnlyList<ResolvedStatement> statements)
    {
        var covered = 0;
        var total = 0;
        var metricGroups = statements
            .Where(HasUsableMethodMetric)
            .GroupBy(statement => statement.MethodKey, StringComparer.Ordinal)
            .ToList();
        var metricKeys = metricGroups.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var group in metricGroups)
        {
            var metric = group.First().MethodMetric!;
            covered += metric.CoveredStatements;
            total += metric.TotalStatements;
        }

        foreach (var statement in statements.Where(statement => statement.MethodKey is null || !metricKeys.Contains(statement.MethodKey)))
        {
            if (statement.Covered)
            {
                covered++;
            }

            total++;
        }

        return new CoverageSummary(covered, total);
    }

    private static CoverageSummary BuildOverallSummary(DotCoverReport dotCover, IReadOnlyList<ResolvedStatement> statements)
    {
        if (dotCover.Root.TotalStatements > 0 && statements.Count == dotCover.Statements.Count)
        {
            return new CoverageSummary(dotCover.Root.CoveredStatements, dotCover.Root.TotalStatements, dotCover.Root.CoveragePercent);
        }

        return SummaryFromDistinctMethods(statements);
    }

    private static bool HasUsableMethodMetric(ResolvedStatement statement)
    {
        return !string.IsNullOrWhiteSpace(statement.MethodKey) && statement.MethodMetric?.TotalStatements > 0;
    }

    private static CoverageRankings BuildRankings(
        IReadOnlyList<CoverageTreeItem> tree,
        IReadOnlyList<MemberCoverageReport> members,
        IReadOnlyList<FileCoverageReport> files)
    {
        var flat = tree.Flatten().ToList();
        return new CoverageRankings(
            RankTree(flat, CoverageTreeKind.Namespace),
            RankTree(flat, CoverageTreeKind.Type),
            members.Where(member => member.Summary.TotalStatements > 0)
                .OrderBy(member => member.Summary.CoveragePercent)
                .ThenByDescending(member => member.Summary.UncoveredStatements)
                .Take(10)
                .ToList(),
            files.Where(file => file.Summary.TotalStatements > 0)
                .OrderByDescending(file => file.Summary.UncoveredStatements)
                .ThenBy(file => file.Summary.CoveragePercent)
                .Take(10)
                .ToList());
    }

    private static IReadOnlyList<CoverageTreeItem> RankTree(IEnumerable<CoverageTreeItem> items, CoverageTreeKind kind)
    {
        return items
            .Where(item => item.Kind == kind && item.Summary.TotalStatements > 0)
            .OrderBy(item => item.Summary.CoveragePercent)
            .ThenByDescending(item => item.Summary.UncoveredStatements)
            .Take(10)
            .ToList();
    }

    private static string ScopeLabel(string projectRoot, CoverageSelection selection)
    {
        return selection.ScopeType switch
        {
            CoverageScopeType.Project => "プロジェクト全体",
            CoverageScopeType.Folder when !string.IsNullOrWhiteSpace(selection.ScopePath) => $"フォルダ: {PathUtilities.GetRelativePath(projectRoot, selection.ScopePath)}",
            CoverageScopeType.File when !string.IsNullOrWhiteSpace(selection.ScopePath) => $"ファイル: {PathUtilities.GetRelativePath(projectRoot, selection.ScopePath)}",
            _ => "プロジェクト全体"
        };
    }

    private sealed record ResolvedStatement(
        string FullPath,
        string RelativePath,
        int Line,
        int EndLine,
        bool Covered,
        string AssemblyName,
        string NamespaceName,
        string TypeName,
        string MethodName,
        string? MethodKey,
        CoverageMetric? MethodMetric);

    private sealed record SourceLineRange(int StartLine, int EndLine);
}
