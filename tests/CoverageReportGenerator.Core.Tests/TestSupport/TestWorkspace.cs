namespace CoverageReportGenerator.Core.Tests.TestSupport;

internal sealed class TestWorkspace : IDisposable
{
    private TestWorkspace(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TestWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "crg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    public string Write(string relativePath, string contents)
    {
        var path = PathOf(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents.ReplaceLineEndings(Environment.NewLine));
        return path;
    }

    public string WriteBytes(string relativePath, byte[] contents)
    {
        var path = PathOf(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, contents);
        return path;
    }

    public string CreateDirectory(string relativePath)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string PathOf(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Root, relativePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for test files that may still be observed by the OS.
        }
    }
}
