using CoverageReportGenerator.Core.DotCover;
using CoverageReportGenerator.Core.Tests.TestSupport;
using System.Text;

namespace CoverageReportGenerator.Core.Tests;

public sealed class DotCoverDetailedXmlParserTests
{
    [Fact]
    public void Parse_reads_file_indices_and_statements_without_depending_on_element_order()
    {
        const string xml = """
            <Root CoveredStatements="2" TotalStatements="3" CoveragePercent="66.7">
              <Assembly Name="Web" CoveredStatements="2" TotalStatements="3" CoveragePercent="66.7">
                <Namespace Name="Sample.Pages" CoveredStatements="2" TotalStatements="3" CoveragePercent="66.7">
                  <Type Name="IndexModel" CoveredStatements="2" TotalStatements="3" CoveragePercent="66.7">
                    <Method Name="OnGetAsync():System.Threading.Tasks.Task" CoveredStatements="1" TotalStatements="1" CoveragePercent="100">
                      <Statement FileIndex="1" Line="11" Covered="True" />
                    </Method>
                    <Method Name="&lt;OnPostAsync&gt;d__4.MoveNext():System.Void" CoveredStatements="1" TotalStatements="2" CoveragePercent="50">
                      <Statement FileIndex="1" Line="16" Covered="False" />
                      <Statement FileIndex="1" Line="17" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
              <FileIndices>
                <File Index="1" Name="Pages\Index.cshtml.cs" />
              </FileIndices>
            </Root>
            """;

        var report = new DotCoverDetailedXmlParser().Parse(xml);

        Assert.Equal(2, report.Root.CoveredStatements);
        Assert.Equal(3, report.Root.TotalStatements);
        Assert.Single(report.Files);
        Assert.Equal(@"Pages\Index.cshtml.cs", report.Files[0].Name);
        Assert.Equal(3, report.Statements.Count);
        Assert.Equal("OnGetAsync():System.Threading.Tasks.Task", report.Statements[0].MethodName);
        Assert.Equal("<OnPostAsync>d__4.MoveNext():System.Void", report.Statements[1].MethodName);
        Assert.Equal("Sample.Pages", report.Statements[2].NamespaceName);
    }

    [Fact]
    public void Parse_fails_when_statement_does_not_have_required_covered_attribute()
    {
        const string xml = """
            <Root>
              <FileIndices><File Index="1" Name="Pages\Index.cshtml.cs" /></FileIndices>
              <Assembly Name="Web">
                <Namespace Name="Sample.Pages">
                  <Type Name="IndexModel">
                    <Method Name="OnGet()">
                      <Statement FileIndex="1" Line="10" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;

        var ex = Assert.Throws<DotCoverParseException>(() => new DotCoverDetailedXmlParser().Parse(xml));
        Assert.Contains("Covered", ex.Message);
    }

    [Fact]
    public void Parse_uses_unknown_for_missing_optional_hierarchy()
    {
        const string xml = """
            <Root>
              <FileIndices><File Index="1" Name="OnlyFile.cs" /></FileIndices>
              <Statement FileIndex="1" Line="4" Covered="True" />
            </Root>
            """;

        var report = new DotCoverDetailedXmlParser().Parse(xml);

        var statement = Assert.Single(report.Statements);
        Assert.Equal("Unknown Assembly", statement.AssemblyName);
        Assert.Equal("Unknown Namespace", statement.NamespaceName);
        Assert.Equal("Unknown Type", statement.TypeName);
        Assert.Equal("Unknown Method", statement.MethodName);
    }

    [Fact]
    public void ParseFile_respects_shift_jis_xml_encoding_declaration()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var workspace = TestWorkspace.Create();
        const string xml = """
            <?xml version="1.0" encoding="shift_jis"?>
            <Root>
              <FileIndices><File Index="1" Name="Pages\一覧.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="サンプル.Pages">
                  <Type Name="一覧Model">
                    <Method Name="OnGet():System.Void">
                      <Statement FileIndex="1" Line="10" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;
        var path = workspace.WriteBytes("dotcover.xml", Encoding.GetEncoding("shift_jis").GetBytes(xml.ReplaceLineEndings(Environment.NewLine)));

        var report = new DotCoverDetailedXmlParser().ParseFile(path);

        Assert.Equal(@"Pages\一覧.cshtml.cs", Assert.Single(report.Files).Name);
        var statement = Assert.Single(report.Statements);
        Assert.Equal("サンプル.Pages", statement.NamespaceName);
        Assert.Equal("一覧Model", statement.TypeName);
    }

    [Fact]
    public void ParseFile_falls_back_to_shift_jis_when_xml_has_no_encoding_declaration()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var workspace = TestWorkspace.Create();
        const string xml = """
            <Root>
              <FileIndices><File Index="1" Name="Pages\詳細.cshtml.cs" /></FileIndices>
              <Assembly Name="Sample.Web">
                <Namespace Name="サンプル.Pages">
                  <Type Name="詳細Model">
                    <Method Name="OnGet():System.Void">
                      <Statement FileIndex="1" Line="10" Covered="True" />
                    </Method>
                  </Type>
                </Namespace>
              </Assembly>
            </Root>
            """;
        var path = workspace.WriteBytes("dotcover-no-declaration.xml", Encoding.GetEncoding("shift_jis").GetBytes(xml.ReplaceLineEndings(Environment.NewLine)));

        var report = new DotCoverDetailedXmlParser().ParseFile(path);

        Assert.Equal(@"Pages\詳細.cshtml.cs", Assert.Single(report.Files).Name);
        var statement = Assert.Single(report.Statements);
        Assert.Equal("サンプル.Pages", statement.NamespaceName);
        Assert.Equal("詳細Model", statement.TypeName);
    }
}
