using System.Text.Json;

namespace CoverageReportGenerator.Core.Settings;

/// <summary>
/// 画面入力値をローカルJSONとして保存・復元する。
/// </summary>
public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    /// <summary>
    /// 既定の設定ファイルを使用する。
    /// </summary>
    public AppSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoverageReportGenerator", "settings.json"))
    {
    }

    /// <summary>
    /// 設定ファイルのパスを指定して生成する。
    /// </summary>
    public AppSettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    /// <summary>
    /// 保存済み設定を読み込む。読込不能な場合は既定値を返す。
    /// </summary>
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

    /// <summary>
    /// 現在の設定を保存する。
    /// </summary>
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

    /// <summary>
    /// 保存済み設定を削除する。
    /// </summary>
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
