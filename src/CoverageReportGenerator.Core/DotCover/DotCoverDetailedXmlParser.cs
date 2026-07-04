using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.DotCover;

/// <summary>
/// dotCover DetailedXMLをレポート生成用モデルへ変換する。
/// </summary>
public sealed class DotCoverDetailedXmlParser
{
    static DotCoverDetailedXmlParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// XMLファイルを読み込み解析する。
    /// </summary>
    public DotCoverReport ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = File.OpenRead(path);
            return ParseDocument(XDocument.Load(stream, LoadOptions.None));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or NotSupportedException)
        {
            // XML宣言だけで判定できないShift_JIS系ファイルは、独自の文字コード判定で再読込する。
            try
            {
                return Parse(SourceTextReader.ReadAllText(path));
            }
            catch (Exception fallbackEx) when (fallbackEx is System.Xml.XmlException or InvalidOperationException or DecoderFallbackException)
            {
                throw new DotCoverParseException("DotCover XML could not be parsed.", fallbackEx);
            }
        }
    }

    /// <summary>
    /// XML文字列を解析する。
    /// </summary>
    public DotCoverReport Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            throw new DotCoverParseException("DotCover XML could not be parsed.", ex);
        }

        return ParseDocument(document);
    }

    private static DotCoverReport ParseDocument(XDocument document)
    {
        var root = document.Root ?? throw new DotCoverParseException("DotCover XML does not have a root element.");
        // FileIndicesはRoot直下以外に出ても拾えるよう、XML全体から探索する。
        var fileIndices = root.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "FileIndices")
            .SelectMany(element => element.Elements().Where(child => child.Name.LocalName == "File"))
            .Select(ParseFileIndex)
            .ToList();

        if (fileIndices.Count == 0)
        {
            throw new DotCoverParseException("DotCover XML must contain FileIndices/File elements.");
        }

        var methodKeys = root.Descendants()
            .Where(element => element.Name.LocalName == "Method")
            .Select((element, index) => (Element: element, Key: $"method-{index + 1}"))
            .ToDictionary(item => item.Element, item => item.Key);

        var statements = root.Descendants()
            .Where(element => element.Name.LocalName == "Statement")
            .Select(element => ParseStatement(element, methodKeys))
            .ToList();

        return new DotCoverReport(ParseMetric(root), fileIndices, statements);
    }

    private static DotCoverFileIndex ParseFileIndex(XElement element)
    {
        var index = RequiredAttribute(element, "Index");
        var name = RequiredAttribute(element, "Name");
        return new DotCoverFileIndex(index, name);
    }

    private static DotCoverStatement ParseStatement(XElement element, IReadOnlyDictionary<XElement, string> methodKeys)
    {
        var fileIndex = RequiredAttribute(element, "FileIndex");
        var lineText = RequiredAttribute(element, "Line");
        var coveredText = RequiredAttribute(element, "Covered");

        if (!int.TryParse(lineText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line) || line < 1)
        {
            throw new DotCoverParseException($"Statement Line must be a positive integer. Actual value: '{lineText}'.");
        }

        if (!bool.TryParse(coveredText, out var covered))
        {
            throw new DotCoverParseException($"Statement Covered must be True or False. Actual value: '{coveredText}'.");
        }

        var endLine = OptionalPositiveInt(element, "EndLine") ?? line;
        if (endLine < line)
        {
            throw new DotCoverParseException($"Statement EndLine must be greater than or equal to Line. Line: '{line}', EndLine: '{endLine}'.");
        }

        var method = FindAncestor(element, "Method");
        var methodKey = method is not null && methodKeys.TryGetValue(method, out var key) ? key : null;

        return new DotCoverStatement(
            fileIndex,
            line,
            covered,
            FindAncestorName(element, "Assembly", "Unknown Assembly"),
            FindAncestorName(element, "Namespace", "Unknown Namespace"),
            FindAncestorName(element, "Type", "Unknown Type"),
            FindAncestorName(element, "Method", "Unknown Method"),
            OptionalPositiveInt(element, "Column"),
            endLine,
            OptionalPositiveInt(element, "EndColumn"),
            methodKey,
            method is null ? null : ParseMetric(method));
    }

    private static CoverageMetric ParseMetric(XElement element)
    {
        return new CoverageMetric(
            OptionalInt(element, "CoveredStatements"),
            OptionalInt(element, "TotalStatements"),
            OptionalDecimal(element, "CoveragePercent"));
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        var attribute = element.Attribute(name);
        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value))
        {
            throw new DotCoverParseException($"{element.Name.LocalName} element must contain '{name}' attribute.");
        }

        return attribute.Value;
    }

    private static string FindAncestorName(XElement element, string ancestorLocalName, string fallback)
    {
        var match = FindAncestor(element, ancestorLocalName);

        var value = match?.Attribute("Name")?.Value;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static XElement? FindAncestor(XElement element, string ancestorLocalName)
    {
        return element.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == ancestorLocalName);
    }

    private static int OptionalInt(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static int? OptionalPositiveInt(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 1)
        {
            throw new DotCoverParseException($"Statement {name} must be a positive integer. Actual value: '{value}'.");
        }

        return result;
    }

    private static decimal? OptionalDecimal(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}
