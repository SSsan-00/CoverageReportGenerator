using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// プロジェクト解析結果をローカルJSONキャッシュとして保存する。
/// </summary>
public sealed class ProjectCacheService
{
    private const int SchemaVersion = 2;
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    /// <summary>
    /// 既定のキャッシュ保存先を使用する。
    /// </summary>
    public ProjectCacheService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoverageReportGenerator", "cache"))
    {
    }

    /// <summary>
    /// キャッシュ保存先を指定して生成する。
    /// </summary>
    public ProjectCacheService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    /// <summary>
    /// プロジェクトに対応するキャッシュを読み込む。
    /// </summary>
    public async Task<ProjectCacheEntry?> LoadAsync(ProjectSourceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(snapshot.ProjectPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var entry = await JsonSerializer.DeserializeAsync<ProjectCacheEntry>(stream, _serializerOptions, cancellationToken);
            return entry?.SchemaVersion == SchemaVersion && PathUtilities.PathComparer.Equals(entry.ProjectPath, snapshot.ProjectPath)
                ? entry
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 解析結果キャッシュを保存する。
    /// </summary>
    public async Task SaveAsync(ProjectCacheEntry entry, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var path = GetCachePath(entry.ProjectPath);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, entry, _serializerOptions, cancellationToken);
    }

    /// <summary>
    /// 現在の解析結果から保存用エントリを作る。
    /// </summary>
    public ProjectCacheEntry CreateEntry(ProjectAnalysis analysis)
    {
        return new ProjectCacheEntry(
            SchemaVersion,
            analysis.ProjectPath,
            analysis.ProjectName,
            analysis.ProjectRoot,
            Metadata(analysis.ProjectPath),
            analysis.SourceFiles.Select(file => new CachedSourceFile(file.FullPath, file.RelativePath, file.Extension, Metadata(file.FullPath))).ToList(),
            analysis.Members.ToList());
    }

    /// <summary>
    /// キャッシュ内のプロジェクトとソース情報が現在の状態と一致するか判定する。
    /// </summary>
    public bool IsProjectMetadataValid(ProjectSourceSnapshot snapshot, ProjectCacheEntry entry)
    {
        return SameMetadata(Metadata(snapshot.ProjectPath), entry.ProjectMetadata)
            && snapshot.SourceFiles.Count == entry.SourceFiles.Count
            && snapshot.SourceFiles.All(file =>
            {
                var cached = entry.SourceFiles.FirstOrDefault(item => PathUtilities.PathComparer.Equals(item.FullPath, file.FullPath));
                return cached is not null && SameMetadata(Metadata(file.FullPath), cached.Metadata);
            });
    }

    /// <summary>
    /// ファイル更新日時とサイズを取得する。
    /// </summary>
    public static FileMetadata Metadata(string path)
    {
        var info = new FileInfo(path);
        return new FileMetadata(info.LastWriteTimeUtc, info.Length);
    }

    /// <summary>
    /// ファイルメタデータが一致するか判定する。
    /// </summary>
    public static bool SameMetadata(FileMetadata left, FileMetadata right)
    {
        return left.LastWriteTimeUtc == right.LastWriteTimeUtc && left.Length == right.Length;
    }

    private string GetCachePath(string projectPath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(projectPath.ToLowerInvariant()));
        var name = Convert.ToHexString(bytes).ToLowerInvariant();
        return Path.Combine(_cacheDirectory, $"{name}.json");
    }
}

/// <summary>
/// キャッシュ検証用のファイルメタデータ。
/// </summary>
public sealed record FileMetadata(DateTime LastWriteTimeUtc, long Length);

/// <summary>
/// キャッシュ内に保存するソースファイル情報。
/// </summary>
public sealed record CachedSourceFile(
    string FullPath,
    string RelativePath,
    string Extension,
    FileMetadata Metadata);

/// <summary>
/// プロジェクト解析キャッシュの保存単位。
/// </summary>
public sealed record ProjectCacheEntry(
    int SchemaVersion,
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    FileMetadata ProjectMetadata,
    IReadOnlyList<CachedSourceFile> SourceFiles,
    IReadOnlyList<SourceMember> Members);
