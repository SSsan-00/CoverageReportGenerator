namespace CoverageReportGenerator.Core.Tests.TestSupport;

/// <summary>
/// テスト用の一時ディレクトリを管理する。
/// </summary>
internal sealed class TestWorkspace : IDisposable
{
    private TestWorkspace(string root)
    {
        Root = root;
    }

    /// <summary>
    /// 一時ディレクトリのルートパス。
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// 新しいテスト用ワークスペースを作る。
    /// </summary>
    public static TestWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "crg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    /// <summary>
    /// テキストファイルを書き込む。
    /// </summary>
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

    /// <summary>
    /// バイト列をファイルへ書き込む。
    /// </summary>
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

    /// <summary>
    /// ディレクトリを作成する。
    /// </summary>
    public string CreateDirectory(string relativePath)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// ワークスペース配下の絶対パスを返す。
    /// </summary>
    public string PathOf(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Root, relativePath));
    }

    /// <summary>
    /// 一時ディレクトリを削除する。
    /// </summary>
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
