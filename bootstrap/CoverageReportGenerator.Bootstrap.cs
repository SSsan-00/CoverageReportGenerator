using System.Diagnostics;
using System.IO.Compression;

const string defaultArchiveUrl = "https://github.com/SSsan-00/CoverageReportGenerator/archive/refs/heads/main.zip";

var options = BootstrapOptions.Parse(args);
var workRoot = Path.Combine(Path.GetTempPath(), "CoverageReportGenerator.Bootstrap", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workRoot);

try
{
    var sourceRoot = options.SourcePath is not null
        ? Path.GetFullPath(options.SourcePath)
        : await DownloadAndExtractAsync(options.RepositoryArchiveUrl ?? defaultArchiveUrl, workRoot);

    var projectPath = Path.Combine(sourceRoot, "src", "CoverageReportGenerator.WinForms", "CoverageReportGenerator.WinForms.csproj");
    if (!File.Exists(projectPath))
    {
        throw new FileNotFoundException("WinForms project was not found.", projectPath);
    }

    Directory.CreateDirectory(options.OutputDirectory);
    Run("dotnet", [
        "publish",
        projectPath,
        "--configuration", "Release",
        "--runtime", "win-x64",
        "--self-contained", "true",
        "/p:PublishSingleFile=true",
        "/p:PublishTrimmed=false",
        "/p:EnableCompressionInSingleFile=true",
        "/p:IncludeNativeLibrariesForSelfExtract=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "-o", options.OutputDirectory
    ]);

    var sourceOutputPath = Path.Combine(options.OutputDirectory, "source");
    CopySourceTree(sourceRoot, sourceOutputPath, options.OutputDirectory);

    Console.WriteLine($"CoverageReportGenerator was published to: {Path.GetFullPath(options.OutputDirectory)}");
    Console.WriteLine($"Source files were copied to: {Path.GetFullPath(sourceOutputPath)}");
}
finally
{
    if (!options.KeepTemp && Directory.Exists(workRoot) && IsChildPath(workRoot, Path.GetTempPath()))
    {
        try
        {
            Directory.Delete(workRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

static async Task<string> DownloadAndExtractAsync(string archiveUrl, string workRoot)
{
    var zipPath = Path.Combine(workRoot, "source.zip");
    var extractPath = Path.Combine(workRoot, "source");
    using var client = new HttpClient();
    await using (var remote = await client.GetStreamAsync(archiveUrl))
    await using (var local = File.Create(zipPath))
    {
        await remote.CopyToAsync(local);
    }

    ZipFile.ExtractToDirectory(zipPath, extractPath);
    var roots = Directory.GetDirectories(extractPath);
    return roots.Length == 1 ? roots[0] : extractPath;
}

static void CopySourceTree(string sourceRoot, string targetRoot, string outputRoot)
{
    var sourceFullPath = Path.GetFullPath(sourceRoot);
    var targetFullPath = Path.GetFullPath(targetRoot);
    var outputFullPath = Path.GetFullPath(outputRoot);
    if (!IsChildPath(targetFullPath, outputFullPath))
    {
        throw new InvalidOperationException($"Source target must be inside the output directory: {targetFullPath}");
    }

    if (Directory.Exists(targetFullPath))
    {
        Directory.Delete(targetFullPath, recursive: true);
    }

    Directory.CreateDirectory(targetFullPath);
    var excludedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "artifacts",
        "bootstrap"
    };

    // 出力先配下を再帰コピー対象から外し、自己コピーを防ぐ。
    CopyDirectoryContent(sourceFullPath, targetFullPath);

    void CopyDirectoryContent(string currentSource, string currentTarget)
    {
        foreach (var directory in Directory.EnumerateDirectories(currentSource))
        {
            var directoryInfo = new DirectoryInfo(directory);
            var directoryFullPath = Path.GetFullPath(directory);
            if (excludedDirectoryNames.Contains(directoryInfo.Name) ||
                string.Equals(directoryFullPath, outputFullPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(directoryFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextTarget = Path.Combine(currentTarget, directoryInfo.Name);
            Directory.CreateDirectory(nextTarget);
            CopyDirectoryContent(directoryFullPath, nextTarget);
        }

        foreach (var file in Directory.EnumerateFiles(currentSource))
        {
            var fileName = Path.GetFileName(file);
            var destination = Path.Combine(currentTarget, fileName);
            if (IsDocumentationFile(sourceFullPath, file))
            {
                var text = File.ReadAllText(file);
                File.WriteAllText(destination, RemoveBootstrapDocumentation(text));
                continue;
            }

            File.Copy(file, destination, overwrite: true);
        }
    }
}

static bool IsDocumentationFile(string sourceRoot, string path)
{
    var relative = Path.GetRelativePath(sourceRoot, path);
    return relative.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
        (relative.StartsWith($"docs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
            Path.GetExtension(relative).Equals(".md", StringComparison.OrdinalIgnoreCase));
}

static string RemoveBootstrapDocumentation(string text)
{
    var lines = System.Text.RegularExpressions.Regex.Split(text, "\r?\n");
    var output = new List<string>();
    var skipHeadingLevel = 0;

    foreach (var line in lines)
    {
        var headingMatch = System.Text.RegularExpressions.Regex.Match(line, "^(#{1,6})\\s+(.+)$");
        if (headingMatch.Success)
        {
            var level = headingMatch.Groups[1].Value.Length;
            var title = headingMatch.Groups[2].Value;
            if (skipHeadingLevel > 0 && level <= skipHeadingLevel)
            {
                skipHeadingLevel = 0;
            }

            if (skipHeadingLevel == 0 && title.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                skipHeadingLevel = level;
                continue;
            }
        }

        if (skipHeadingLevel > 0 || line.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        output.Add(line);
    }

    return string.Join(Environment.NewLine, output).TrimEnd() + Environment.NewLine;
}

static bool IsChildPath(string childPath, string parentPath)
{
    var childFullPath = Path.GetFullPath(childPath);
    var parentFullPath = Path.GetFullPath(parentPath);
    if (!parentFullPath.EndsWith(Path.DirectorySeparatorChar))
    {
        parentFullPath += Path.DirectorySeparatorChar;
    }

    return childFullPath.StartsWith(parentFullPath, StringComparison.OrdinalIgnoreCase);
}

static void Run(string fileName, IReadOnlyList<string> arguments)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
    }
}

/// <summary>
/// Bootstrap実行時のオプション。
/// </summary>
sealed record BootstrapOptions(
    string? SourcePath,
    string? RepositoryArchiveUrl,
    string OutputDirectory,
    bool KeepTemp)
{
    /// <summary>
    /// コマンドライン引数をBootstrapオプションへ変換する。
    /// </summary>
    public static BootstrapOptions Parse(string[] args)
    {
        string? sourcePath = null;
        string? archiveUrl = null;
        var outputDirectory = Path.Combine(Environment.CurrentDirectory, "dist");
        var keepTemp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--source":
                    sourcePath = RequiredValue(args, ref i, arg);
                    break;
                case "--repo-zip":
                    archiveUrl = RequiredValue(args, ref i, arg);
                    break;
                case "--output":
                    outputDirectory = RequiredValue(args, ref i, arg);
                    break;
                case "--keep-temp":
                    keepTemp = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"不明なオプションです: {arg}");
            }
        }

        return new BootstrapOptions(sourcePath, archiveUrl, Path.GetFullPath(outputDirectory), keepTemp);
    }

    private static string RequiredValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} には値が必要です。");
        }

        index++;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            CoverageReportGenerator Bootstrap

            オプション:
              --output <path>    publish先フォルダ。既定値: ./dist
              --source <path>    既存のローカルソースフォルダを使用します。
              --repo-zip <url>   リポジトリzipをダウンロードします。既定値: public mainブランチのzip。
              --keep-temp        一時ダウンロード/展開ファイルを残します。
            """);
    }
}
