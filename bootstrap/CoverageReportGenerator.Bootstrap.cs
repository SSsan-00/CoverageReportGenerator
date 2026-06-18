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

    Console.WriteLine($"CoverageReportGenerator was published to: {Path.GetFullPath(options.OutputDirectory)}");
}
finally
{
    if (!options.KeepTemp && Directory.Exists(workRoot))
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

sealed record BootstrapOptions(
    string? SourcePath,
    string? RepositoryArchiveUrl,
    string OutputDirectory,
    bool KeepTemp)
{
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
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        return new BootstrapOptions(sourcePath, archiveUrl, Path.GetFullPath(outputDirectory), keepTemp);
    }

    private static string RequiredValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            CoverageReportGenerator Bootstrap

            Options:
              --output <path>    Publish output directory. Default: ./dist
              --source <path>    Use an existing local source directory.
              --repo-zip <url>   Download a repository zip archive. Default: public main branch archive.
              --keep-temp        Keep temporary download/extraction files.
            """);
    }
}
