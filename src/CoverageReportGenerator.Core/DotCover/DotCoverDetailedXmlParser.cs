using System.Globalization;
using System.Text;
using System.Xml.Linq;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.DotCover;

public sealed class DotCoverDetailedXmlParser
{
    static DotCoverDetailedXmlParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

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
        var fileIndices = root.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "FileIndices")
            .SelectMany(element => element.Elements().Where(child => child.Name.LocalName == "File"))
            .Select(ParseFileIndex)
            .ToList();

        if (fileIndices.Count == 0)
        {
            throw new DotCoverParseException("DotCover XML must contain FileIndices/File elements.");
        }

        var statements = root.Descendants()
            .Where(element => element.Name.LocalName == "Statement")
            .Select(ParseStatement)
            .ToList();

        return new DotCoverReport(ParseMetric(root), fileIndices, statements);
    }

    private static DotCoverFileIndex ParseFileIndex(XElement element)
    {
        var index = RequiredAttribute(element, "Index");
        var name = RequiredAttribute(element, "Name");
        return new DotCoverFileIndex(index, name);
    }

    private static DotCoverStatement ParseStatement(XElement element)
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

        return new DotCoverStatement(
            fileIndex,
            line,
            covered,
            FindAncestorName(element, "Assembly", "Unknown Assembly"),
            FindAncestorName(element, "Namespace", "Unknown Namespace"),
            FindAncestorName(element, "Type", "Unknown Type"),
            FindAncestorName(element, "Method", "Unknown Method"));
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
        var match = element.Ancestors()
            .FirstOrDefault(ancestor => ancestor.Name.LocalName == ancestorLocalName);

        var value = match?.Attribute("Name")?.Value;
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int OptionalInt(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static decimal? OptionalDecimal(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}
