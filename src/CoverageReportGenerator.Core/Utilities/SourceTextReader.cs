using System.Text;

namespace CoverageReportGenerator.Core.Utilities;

/// <summary>
/// UTF-8/UTF-16/CP932のソーステキストを読み込む。
/// </summary>
internal static class SourceTextReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static SourceTextReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// ファイル全体を非同期で文字列として読み込む。
    /// </summary>
    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Decode(bytes);
    }

    /// <summary>
    /// ファイル全体を文字列として読み込む。
    /// </summary>
    public static string ReadAllText(string path)
    {
        return Decode(File.ReadAllBytes(path));
    }

    /// <summary>
    /// ファイルを行単位で読み込む。
    /// </summary>
    public static string[] ReadAllLines(string path)
    {
        return SplitLines(Decode(File.ReadAllBytes(path)));
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            var utf8Text = StrictUtf8.GetString(bytes);
            if (!ContainsSuspiciousControlCharacters(utf8Text))
            {
                return utf8Text;
            }

            // UTF-8として読めても制御文字が出る場合は、CP932としての自然さを比較する。
            var cp932Text = Encoding.GetEncoding(932).GetString(bytes);
            return CountSuspiciousControlCharacters(cp932Text) < CountSuspiciousControlCharacters(utf8Text)
                ? cp932Text
                : utf8Text;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(932).GetString(bytes);
        }
    }

    private static bool ContainsSuspiciousControlCharacters(string text)
    {
        return CountSuspiciousControlCharacters(text) > 0;
    }

    private static int CountSuspiciousControlCharacters(string text)
    {
        return text.Count(ch => ch is >= '\u0080' and <= '\u009F');
    }

    private static string[] SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        return normalized.EndsWith('\n') ? lines[..^1] : lines;
    }
}
