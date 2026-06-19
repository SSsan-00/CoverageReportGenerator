using System.Globalization;
using System.Net;
using System.Text;
using CoverageReportGenerator.Core.Reports;

namespace CoverageReportGenerator.Core.Rendering;

public sealed class HtmlReportRenderer
{
    private static readonly UTF8Encoding HtmlEncoding = new(encoderShouldEmitUTF8Identifier: true);

    public void RenderToFile(CoverageReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, Render(report), HtmlEncoding);
    }

    public string Render(CoverageReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>").Append(E(report.ReportTitle)).AppendLine("</title>");
        html.AppendLine("<style>");
        html.AppendLine(Styles);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        RenderHeader(html, report);
        RenderTabs(html, report);
        html.AppendLine("<script>");
        html.AppendLine(Script);
        html.AppendLine("</script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void RenderHeader(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<header class=\"page-header\">");
        html.Append("<h1>").Append(E(report.ReportTitle)).AppendLine("</h1>");
        html.Append("<div class=\"meta\">Project: ").Append(E(report.ProjectName))
            .Append(" · Scope: ").Append(E(report.ScopeLabel))
            .Append(" · Generated: ").Append(E(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)))
            .AppendLine("</div>");
        html.AppendLine("<section class=\"summary-grid\">");
        SummaryCard(html, "Coverage", $"{report.Summary.CoveragePercent:0.0}%");
        SummaryCard(html, "Covered", report.Summary.CoveredStatements.ToString(CultureInfo.InvariantCulture));
        SummaryCard(html, "Total", report.Summary.TotalStatements.ToString(CultureInfo.InvariantCulture));
        SummaryCard(html, "Files", report.Files.Count.ToString(CultureInfo.InvariantCulture));
        html.AppendLine("</section>");
        html.AppendLine("</header>");
    }

    private static void SummaryCard(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"summary-card\"><span>").Append(E(label)).Append("</span><strong>")
            .Append(E(value)).AppendLine("</strong></div>");
    }

    private static void RenderTabs(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<nav class=\"tabs\">");
        foreach (var (id, label) in new[] { ("summary", "Summary"), ("rankings", "Rankings"), ("members", "Members"), ("files", "Files"), ("source", "Source") })
        {
            html.Append("<button type=\"button\" class=\"tab-button\" data-tab=\"").Append(id).Append("\">").Append(label).AppendLine("</button>");
        }
        html.AppendLine("</nav>");

        html.AppendLine("<main>");
        html.AppendLine("<section id=\"tab-summary\" class=\"tab-panel\">");
        RenderSummary(html, report);
        html.AppendLine("</section>");

        html.AppendLine("<section id=\"tab-rankings\" class=\"tab-panel\">");
        RenderRankings(html, report);
        html.AppendLine("</section>");

        html.AppendLine("<section id=\"tab-members\" class=\"tab-panel\">");
        RenderMembers(html, report);
        html.AppendLine("</section>");

        html.AppendLine("<section id=\"tab-files\" class=\"tab-panel\">");
        RenderFiles(html, report);
        html.AppendLine("</section>");

        html.AppendLine("<section id=\"tab-source\" class=\"tab-panel\">");
        RenderSource(html, report);
        html.AppendLine("</section>");

        html.AppendLine("</main>");
    }

    private static void RenderSummary(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>Summary</h2></div>");
        html.AppendLine("<table><tbody>");
        Row(html, "Report", report.ReportTitle);
        Row(html, "Project", report.ProjectName);
        Row(html, "Project file", report.ProjectPath);
        Row(html, "Scope", report.ScopeLabel);
        Row(html, "Coverage", $"{report.Summary.CoveragePercent:0.0}%");
        Row(html, "Statements", $"{report.Summary.CoveredStatements}/{report.Summary.TotalStatements}");
        Row(html, "Target files", report.Files.Count.ToString(CultureInfo.InvariantCulture));
        html.AppendLine("</tbody></table>");
    }

    private static void RenderRankings(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"rank-grid\">");
        RankingTree(html, "Lowest Namespaces", report.Rankings.LowestNamespaces);
        RankingTree(html, "Lowest Types", report.Rankings.LowestTypes);
        RankingMembers(html, "Lowest Members", report.Rankings.LowestMembers);
        RankingFiles(html, "Most Uncovered Files", report.Rankings.MostUncoveredFiles);
        html.AppendLine("</div>");
    }

    private static void RankingTree(StringBuilder html, string title, IReadOnlyList<CoverageTreeItem> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>Coverage</th><th>Statements</th><th>Name</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr><td>").Append(Percent(item.Summary)).Append("</td><td>").Append(Statements(item.Summary)).Append("</td><td>")
                .Append(E(item.Name)).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RankingMembers(StringBuilder html, string title, IReadOnlyList<MemberCoverageReport> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>Coverage</th><th>Statements</th><th>Member</th><th>File</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr><td>").Append(Percent(item.Summary)).Append("</td><td>").Append(Statements(item.Summary)).Append("</td><td>");
            LinkToSource(html, item.FileId, item.StartLine, item.DisplayName);
            html.Append("</td><td>").Append(E(item.RelativePath)).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RankingFiles(StringBuilder html, string title, IReadOnlyList<FileCoverageReport> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>Uncovered</th><th>Coverage</th><th>File</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr><td>").Append(item.Summary.UncoveredStatements).Append("</td><td>").Append(Percent(item.Summary)).Append("</td><td>");
            LinkToSource(html, item.FileId, 1, item.RelativePath);
            html.AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RenderMembers(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>Members</h2><input class=\"filter\" placeholder=\"Filter members\" data-filter-table=\"members-table\"></div>");
        html.AppendLine("<table id=\"members-table\"><thead><tr><th>Coverage</th><th>Statements</th><th>File</th><th>Type</th><th>Member</th><th>Kind</th><th>Lines</th></tr></thead><tbody>");
        foreach (var member in report.Members)
        {
            html.Append("<tr><td>").Append(Percent(member.Summary)).Append("</td><td>").Append(Statements(member.Summary)).Append("</td><td>")
                .Append(E(member.RelativePath)).Append("</td><td>").Append(E(member.ContainingType)).Append("</td><td>");
            LinkToSource(html, member.FileId, member.StartLine, member.DisplayName);
            html.Append("</td><td>").Append(member.Kind).Append("</td><td>").Append(member.StartLine).Append("-").Append(member.EndLine).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void RenderFiles(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>Files</h2><input class=\"filter\" placeholder=\"Filter files\" data-filter-table=\"files-table\"></div>");
        html.AppendLine("<table id=\"files-table\"><thead><tr><th>Coverage</th><th>Statements</th><th>File</th><th>Source</th></tr></thead><tbody>");
        foreach (var file in report.Files)
        {
            html.Append("<tr><td>").Append(Percent(file.Summary)).Append("</td><td>").Append(Statements(file.Summary)).Append("</td><td>");
            LinkToSource(html, file.FileId, 1, file.RelativePath);
            html.Append("</td><td>").Append(file.SourceFound ? "Found" : "Not found").AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void RenderSource(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>Source</h2><input class=\"filter\" placeholder=\"Filter source files\" data-filter-details=\"source\"></div>");
        var singleFile = report.Files.Count == 1;
        foreach (var file in report.Files)
        {
            html.Append("<details id=\"source-file-").Append(file.FileId).Append("\" class=\"source-file\"");
            if (singleFile)
            {
                html.Append(" open");
            }

            html.Append("><summary>").Append(E(file.RelativePath)).Append(" · ").Append(Percent(file.Summary)).Append(" · ")
                .Append(Statements(file.Summary)).AppendLine("</summary>");
            html.AppendLine("<table class=\"source-table\"><tbody>");
            if (file.Lines.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"3\">source file not found</td></tr>");
            }
            else
            {
                foreach (var line in file.Lines)
                {
                    html.Append("<tr id=\"src-file-").Append(file.FileId).Append("-line-").Append(line.LineNumber).Append("\" class=\"line-")
                        .Append(StatusClass(line.Status)).Append("\"><td class=\"line-number\">").Append(line.LineNumber)
                        .Append("</td><td class=\"line-status\">").Append(StatusLabel(line.Status))
                        .Append("</td><td><pre>").Append(E(line.Text)).AppendLine("</pre></td></tr>");
                }
            }

            html.AppendLine("</tbody></table></details>");
        }
    }

    private static void Row(StringBuilder html, string key, string value)
    {
        html.Append("<tr><th>").Append(E(key)).Append("</th><td>").Append(E(value)).AppendLine("</td></tr>");
    }

    private static void LinkToSource(StringBuilder html, int fileId, int line, string text)
    {
        html.Append("<a href=\"#src-file-").Append(fileId).Append("-line-").Append(line)
            .Append("\" onclick=\"jumpToSource(").Append(fileId).Append(", ").Append(line).Append("); return false;\">")
            .Append(E(text)).Append("</a>");
    }

    private static string Percent(CoverageSummary summary)
    {
        return $"{summary.CoveragePercent:0.0}%";
    }

    private static string Statements(CoverageSummary summary)
    {
        return $"{summary.CoveredStatements}/{summary.TotalStatements}";
    }

    private static string StatusClass(LineCoverageStatus status)
    {
        return status switch
        {
            LineCoverageStatus.Covered => "covered",
            LineCoverageStatus.Uncovered => "uncovered",
            _ => "nodata"
        };
    }

    private static string StatusLabel(LineCoverageStatus status)
    {
        return status switch
        {
            LineCoverageStatus.Covered => "C",
            LineCoverageStatus.Uncovered => "U",
            _ => string.Empty
        };
    }

    private static string E(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private const string Styles = """
        :root { color-scheme: light; --ok:#d7f4dd; --bad:#ffdcdc; --muted:#f5f7fa; --ink:#1f2937; --line:#d7dde5; --accent:#2563eb; }
        body { margin:0; color:var(--ink); font-family:Segoe UI, Arial, sans-serif; background:#ffffff; }
        .page-header { padding:20px 24px 12px; border-bottom:1px solid var(--line); background:#f9fafb; }
        h1 { margin:0 0 6px; font-size:24px; letter-spacing:0; }
        h2 { margin:0 0 10px; font-size:18px; letter-spacing:0; }
        .meta { color:#536171; font-size:13px; }
        .summary-grid { display:grid; grid-template-columns:repeat(4,minmax(120px,1fr)); gap:12px; margin-top:16px; }
        .summary-card { background:#fff; border:1px solid var(--line); border-radius:8px; padding:12px; }
        .summary-card span { display:block; font-size:12px; color:#536171; }
        .summary-card strong { display:block; font-size:22px; margin-top:3px; }
        .tabs { position:sticky; top:0; z-index:10; display:flex; gap:4px; padding:8px 24px; border-bottom:1px solid var(--line); background:#fff; }
        .tab-button { border:1px solid var(--line); background:#fff; border-radius:6px; padding:8px 12px; cursor:pointer; }
        .tab-button.active { color:#fff; border-color:var(--accent); background:var(--accent); }
        main { padding:18px 24px 40px; }
        .tab-panel { display:none; }
        .tab-panel.active { display:block; }
        .panel-heading { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-bottom:12px; }
        .filter { min-width:240px; padding:8px 10px; border:1px solid var(--line); border-radius:6px; }
        .rank-grid { display:grid; grid-template-columns:repeat(2,minmax(260px,1fr)); gap:18px; }
        table { width:100%; border-collapse:collapse; background:#fff; border:1px solid var(--line); }
        th, td { border-bottom:1px solid var(--line); padding:7px 9px; text-align:left; vertical-align:top; font-size:13px; }
        th { background:#f3f6f9; color:#374151; font-weight:600; }
        a { color:var(--accent); text-decoration:none; }
        a:hover { text-decoration:underline; }
        details.source-file { margin:0 0 12px; border:1px solid var(--line); border-radius:8px; overflow:hidden; }
        details.source-file > summary { cursor:pointer; padding:10px 12px; background:#f3f6f9; font-weight:600; }
        .source-table { border:0; font-family:Consolas, 'Cascadia Mono', monospace; }
        .source-table td { padding:0 8px; font-size:12px; border-bottom:0; }
        .source-table pre { margin:0; white-space:pre-wrap; font-family:inherit; }
        .line-number { width:56px; text-align:right; color:#6b7280; user-select:none; }
        .line-status { width:24px; text-align:center; font-weight:700; }
        .line-covered { background:var(--ok); }
        .line-uncovered { background:var(--bad); }
        .line-nodata { background:#fff; }
        .flash { outline:2px solid var(--accent); outline-offset:-2px; }
        @media (max-width: 860px) { .summary-grid, .rank-grid { grid-template-columns:1fr; } .tabs { flex-wrap:wrap; } }
        """;

    private const string Script = """
        const buttons = Array.from(document.querySelectorAll('.tab-button'));
        const panels = Array.from(document.querySelectorAll('.tab-panel'));
        function activateTab(id) {
          buttons.forEach(b => b.classList.toggle('active', b.dataset.tab === id));
          panels.forEach(p => p.classList.toggle('active', p.id === 'tab-' + id));
        }
        buttons.forEach(b => b.addEventListener('click', () => activateTab(b.dataset.tab)));
        activateTab('summary');
        function jumpToSource(fileId, line) {
          activateTab('source');
          const details = document.getElementById('source-file-' + fileId);
          if (details) details.open = true;
          const row = document.getElementById('src-file-' + fileId + '-line-' + line);
          if (!row) return;
          row.scrollIntoView({ behavior: 'smooth', block: 'center' });
          row.classList.add('flash');
          setTimeout(() => row.classList.remove('flash'), 1800);
        }
        document.querySelectorAll('[data-filter-table]').forEach(input => {
          input.addEventListener('input', () => {
            const table = document.getElementById(input.dataset.filterTable);
            const text = input.value.toLowerCase();
            table.querySelectorAll('tbody tr').forEach(row => row.style.display = row.textContent.toLowerCase().includes(text) ? '' : 'none');
          });
        });
        document.querySelectorAll('[data-filter-details]').forEach(input => {
          input.addEventListener('input', () => {
            const text = input.value.toLowerCase();
            document.querySelectorAll('details.source-file').forEach(item => item.style.display = item.textContent.toLowerCase().includes(text) ? '' : 'none');
          });
        });
        """;
}
