using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Rendering;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Reports;

/// <summary>
/// ファイル単位のExcelカバレッジレポート生成を実行する。
/// </summary>
public sealed class ExcelReportGenerationService
{
    private readonly ProjectAnalyzer _projectAnalyzer;
    private readonly DotCoverDetailedXmlParser _dotCoverParser;
    private readonly CoverageReportBuilder _reportBuilder;
    private readonly ExcelFileCoverageReportRenderer _renderer;

    /// <summary>
    /// 既定の依存関係でサービスを生成する。
    /// </summary>
    public ExcelReportGenerationService()
        : this(new ProjectAnalyzer(), new DotCoverDetailedXmlParser(), new CoverageReportBuilder(), new ExcelFileCoverageReportRenderer())
    {
    }

    /// <summary>
    /// 依存関係を指定してサービスを生成する。
    /// </summary>
    public ExcelReportGenerationService(
        ProjectAnalyzer projectAnalyzer,
        DotCoverDetailedXmlParser dotCoverParser,
        CoverageReportBuilder reportBuilder,
        ExcelFileCoverageReportRenderer renderer)
    {
        _projectAnalyzer = projectAnalyzer;
        _dotCoverParser = dotCoverParser;
        _reportBuilder = reportBuilder;
        _renderer = renderer;
    }

    /// <summary>
    /// 指定オプションでExcelレポートを生成する。
    /// </summary>
    public async Task<ExcelReportGenerationResult> GenerateAsync(
        ExcelReportGenerationOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Validate(options);

        progress?.Report("プロジェクトを読み込み中");
        var analysis = await _projectAnalyzer.AnalyzeAsync(
            options.ProjectPath,
            new Progress<ProjectAnalysisProgress>(item => progress?.Report(item.Message)),
            cancellationToken);

        progress?.Report("DotCover XMLを読み込み中");
        var dotCover = _dotCoverParser.ParseFile(options.DotCoverXmlPath);

        var sourceFilePaths = NormalizeSourceFilePaths(options.SourceFilePaths);
        var reports = new List<CoverageReport>();
        foreach (var sourceFilePath in sourceFilePaths)
        {
            progress?.Report($"Excel用レポートモデルを作成中: {Path.GetFileName(sourceFilePath)}");
            reports.Add(_reportBuilder.Build(new CoverageReportRequest(
                analysis,
                dotCover,
                new CoverageSelection(CoverageScopeType.File, sourceFilePath, options.IncludePatterns, options.ExcludePatterns),
                Path.GetFileName(sourceFilePath),
                DateTimeOffset.Now)));
        }

        progress?.Report($"Excelを書き出し中: {options.OutputPath}");
        _renderer.RenderToFile(reports, options.OutputPath);

        return new ExcelReportGenerationResult(options.OutputPath, reports, analysis.CacheStatus);
    }

    private static void Validate(ExcelReportGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ProjectPath))
        {
            throw new FileNotFoundException("Project file was not found.", options.ProjectPath);
        }

        if (!Path.GetExtension(options.ProjectPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Project analysis requires a .csproj file.", nameof(options));
        }

        if (!File.Exists(options.DotCoverXmlPath))
        {
            throw new FileNotFoundException("DotCover XML file was not found.", options.DotCoverXmlPath);
        }

        if (options.SourceFilePaths is null || options.SourceFilePaths.Count == 0)
        {
            throw new ArgumentException("At least one source file is required.", nameof(options));
        }

        foreach (var sourceFilePath in options.SourceFilePaths)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source file was not found.", sourceFilePath);
            }
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(options));
        }
    }

    private static IReadOnlyList<string> NormalizeSourceFilePaths(IEnumerable<string> sourceFilePaths)
    {
        return sourceFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(PathUtilities.NormalizeFullPath)
            .Distinct(PathUtilities.PathComparer)
            .ToList();
    }
}

/// <summary>
/// Excelレポート生成に必要な入力オプション。
/// </summary>
public sealed record ExcelReportGenerationOptions(
    string ProjectPath,
    string DotCoverXmlPath,
    IReadOnlyList<string> SourceFilePaths,
    string OutputPath,
    string IncludePatterns,
    string ExcludePatterns)
{
    /// <summary>
    /// 単一ファイル指定との互換性を保つ。
    /// </summary>
    public ExcelReportGenerationOptions(
        string projectPath,
        string dotCoverXmlPath,
        string sourceFilePath,
        string outputPath,
        string includePatterns,
        string excludePatterns)
        : this(projectPath, dotCoverXmlPath, [sourceFilePath], outputPath, includePatterns, excludePatterns)
    {
    }
}

/// <summary>
/// Excelレポート生成結果。
/// </summary>
public sealed record ExcelReportGenerationResult(
    string OutputPath,
    IReadOnlyList<CoverageReport> Reports,
    ProjectCacheStatus CacheStatus)
{
    /// <summary>
    /// 単一ファイル利用時に従来どおり先頭レポートを返す。
    /// </summary>
    public CoverageReport Report => Reports[0];
}
