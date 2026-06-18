using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Rendering;

namespace CoverageReportGenerator.Core.Reports;

public sealed class CoverageReportGenerationService
{
    private readonly ProjectAnalyzer _projectAnalyzer;
    private readonly DotCoverDetailedXmlParser _dotCoverParser;
    private readonly CoverageReportBuilder _reportBuilder;
    private readonly HtmlReportRenderer _renderer;

    public CoverageReportGenerationService()
        : this(new ProjectAnalyzer(), new DotCoverDetailedXmlParser(), new CoverageReportBuilder(), new HtmlReportRenderer())
    {
    }

    public CoverageReportGenerationService(
        ProjectAnalyzer projectAnalyzer,
        DotCoverDetailedXmlParser dotCoverParser,
        CoverageReportBuilder reportBuilder,
        HtmlReportRenderer renderer)
    {
        _projectAnalyzer = projectAnalyzer;
        _dotCoverParser = dotCoverParser;
        _reportBuilder = reportBuilder;
        _renderer = renderer;
    }

    public async Task<CoverageReportGenerationResult> GenerateAsync(
        CoverageReportGenerationOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Validate(options);

        progress?.Report("Loading project");
        var analysis = await _projectAnalyzer.AnalyzeAsync(
            options.ProjectPath,
            new Progress<ProjectAnalysisProgress>(item => progress?.Report(item.Message)),
            cancellationToken);

        progress?.Report("Loading DotCover XML");
        var dotCover = _dotCoverParser.ParseFile(options.DotCoverXmlPath);

        progress?.Report("Building report model");
        var report = _reportBuilder.Build(new CoverageReportRequest(
            analysis,
            dotCover,
            new CoverageSelection(options.ScopeType, options.ScopePath, options.IncludePatterns, options.ExcludePatterns),
            options.ReportTitle,
            DateTimeOffset.Now));

        var outputPath = ResolveOutputPath(options, analysis.ProjectName);
        progress?.Report($"Writing {outputPath}");
        _renderer.RenderToFile(report, outputPath);

        return new CoverageReportGenerationResult(outputPath, report, analysis.CacheStatus);
    }

    private static void Validate(CoverageReportGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ProjectPath))
        {
            throw new FileNotFoundException("Project file was not found.", options.ProjectPath);
        }

        if (!File.Exists(options.DotCoverXmlPath))
        {
            throw new FileNotFoundException("DotCover XML file was not found.", options.DotCoverXmlPath);
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(options));
        }
    }

    private static string ResolveOutputPath(CoverageReportGenerationOptions options, string projectName)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var fileName = string.IsNullOrWhiteSpace(projectName) ? "coverage-report" : $"{SafeFileName(projectName)}-coverage-report";
        var outputPath = Path.Combine(options.OutputDirectory, $"{fileName}.html");
        if (options.OverwriteExisting || !File.Exists(outputPath))
        {
            return outputPath;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(options.OutputDirectory, $"{fileName}-{timestamp}.html");
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
    }
}

public sealed record CoverageReportGenerationOptions(
    string ProjectPath,
    string DotCoverXmlPath,
    string OutputDirectory,
    string ReportTitle,
    CoverageScopeType ScopeType,
    string? ScopePath,
    string IncludePatterns,
    string ExcludePatterns,
    bool OverwriteExisting);

public sealed record CoverageReportGenerationResult(
    string OutputPath,
    CoverageReport Report,
    ProjectCacheStatus CacheStatus);
