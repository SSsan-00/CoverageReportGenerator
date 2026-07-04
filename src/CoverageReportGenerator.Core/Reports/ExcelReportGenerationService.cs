using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Rendering;

namespace CoverageReportGenerator.Core.Reports;

/// <summary>
/// 単一ファイルのExcelカバレッジレポート生成を実行する。
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

        progress?.Report("Excel用レポートモデルを作成中");
        var report = _reportBuilder.Build(new CoverageReportRequest(
            analysis,
            dotCover,
            new CoverageSelection(CoverageScopeType.File, options.SourceFilePath, options.IncludePatterns, options.ExcludePatterns),
            Path.GetFileName(options.SourceFilePath),
            DateTimeOffset.Now));

        progress?.Report($"Excelを書き出し中: {options.OutputPath}");
        _renderer.RenderToFile(report, options.OutputPath);

        return new ExcelReportGenerationResult(options.OutputPath, report, analysis.CacheStatus);
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

        if (!File.Exists(options.SourceFilePath))
        {
            throw new FileNotFoundException("Source file was not found.", options.SourceFilePath);
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(options));
        }
    }
}

/// <summary>
/// Excelレポート生成に必要な入力オプション。
/// </summary>
public sealed record ExcelReportGenerationOptions(
    string ProjectPath,
    string DotCoverXmlPath,
    string SourceFilePath,
    string OutputPath,
    string IncludePatterns,
    string ExcludePatterns);

/// <summary>
/// Excelレポート生成結果。
/// </summary>
public sealed record ExcelReportGenerationResult(
    string OutputPath,
    CoverageReport Report,
    ProjectCacheStatus CacheStatus);
