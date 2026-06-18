using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

public sealed class ProjectCacheService
{
    private const int SchemaVersion = 1;
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public ProjectCacheService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoverageReportGenerator", "cache"))
    {
    }

    public ProjectCacheService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

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

    public async Task SaveAsync(ProjectCacheEntry entry, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var path = GetCachePath(entry.ProjectPath);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, entry, _serializerOptions, cancellationToken);
    }

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

    public static FileMetadata Metadata(string path)
    {
        var info = new FileInfo(path);
        return new FileMetadata(info.LastWriteTimeUtc, info.Length);
    }

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

public sealed record FileMetadata(DateTime LastWriteTimeUtc, long Length);

public sealed record CachedSourceFile(
    string FullPath,
    string RelativePath,
    string Extension,
    FileMetadata Metadata);

public sealed record ProjectCacheEntry(
    int SchemaVersion,
    string ProjectPath,
    string ProjectName,
    string ProjectRoot,
    FileMetadata ProjectMetadata,
    IReadOnlyList<CachedSourceFile> SourceFiles,
    IReadOnlyList<SourceMember> Members);
