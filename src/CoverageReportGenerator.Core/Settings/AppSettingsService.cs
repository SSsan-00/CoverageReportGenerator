using System.Text.Json;

namespace CoverageReportGenerator.Core.Settings;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public AppSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoverageReportGenerator", "settings.json"))
    {
    }

    public AppSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Defaults;
        }

        try
        {
            using var stream = File.OpenRead(_settingsPath);
            return Normalize(JsonSerializer.Deserialize<AppSettings>(stream, _serializerOptions));
        }
        catch
        {
            return AppSettings.Defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(_settingsPath);
        JsonSerializer.Serialize(stream, Normalize(settings), _serializerOptions);
    }

    public void Reset()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        if (settings is null)
        {
            return AppSettings.Defaults;
        }

        var defaults = AppSettings.Defaults;
        return settings with
        {
            ProjectPath = settings.ProjectPath ?? defaults.ProjectPath,
            DotCoverXmlPath = settings.DotCoverXmlPath ?? defaults.DotCoverXmlPath,
            ScopePath = settings.ScopePath ?? defaults.ScopePath,
            IncludePatterns = string.IsNullOrWhiteSpace(settings.IncludePatterns) ? defaults.IncludePatterns : settings.IncludePatterns,
            ExcludePatterns = settings.ExcludePatterns ?? defaults.ExcludePatterns,
            OutputDirectory = settings.OutputDirectory ?? defaults.OutputDirectory,
            ReportTitle = settings.ReportTitle ?? defaults.ReportTitle,
            ScopeType = Enum.IsDefined(settings.ScopeType) ? settings.ScopeType : defaults.ScopeType
        };
    }
}
