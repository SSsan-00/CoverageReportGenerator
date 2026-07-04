using System.Globalization;
using System.Net;
using System.Text;
using CoverageReportGenerator.Core.Reports;

namespace CoverageReportGenerator.Core.Rendering;

/// <summary>
/// カバレッジレポートを単一HTML文字列へ描画する。
/// </summary>
public sealed class HtmlReportRenderer
{
    // 一部環境の文字コード自動判定を安定させるため、HTMLはUTF-8 BOM付きで保存する。
    private static readonly UTF8Encoding HtmlEncoding = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// レポートをHTMLファイルへ書き出す。
    /// </summary>
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

    /// <summary>
    /// レポートをHTML文字列として生成する。
    /// </summary>
    public string Render(CoverageReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"ja\">");
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
        html.Append("<div class=\"meta\">プロジェクト: ").Append(E(report.ProjectName))
            .Append(" · 対象: ").Append(E(report.ScopeLabel))
            .Append(" · 生成日時: ").Append(E(report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)))
            .AppendLine("</div>");
        html.AppendLine("<section class=\"summary-grid\">");
        SummaryCoverageCard(html, "カバレッジ", report.Summary);
        SummaryCard(html, "カバー済み", report.Summary.CoveredStatements.ToString(CultureInfo.InvariantCulture));
        SummaryCard(html, "総数", report.Summary.TotalStatements.ToString(CultureInfo.InvariantCulture));
        SummaryCard(html, "ファイル数", report.Files.Count.ToString(CultureInfo.InvariantCulture));
        html.AppendLine("</section>");
        html.AppendLine("</header>");
    }

    private static void SummaryCard(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"summary-card\"><span>").Append(E(label)).Append("</span><strong>")
            .Append(E(value)).AppendLine("</strong></div>");
    }

    private static void SummaryCoverageCard(StringBuilder html, string label, CoverageSummary summary)
    {
        html.Append("<div class=\"summary-card summary-coverage\"><span>").Append(E(label)).Append("</span>");
        CoverageVisual(html, summary);
        html.AppendLine("</div>");
    }

    private static void RenderTabs(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<nav class=\"tabs\">");
        foreach (var (id, label) in new[] { ("summary", "概要"), ("rankings", "ランキング"), ("members", "メンバー"), ("files", "ファイル"), ("source", "ソース") })
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
        html.AppendLine("<div class=\"panel-heading\"><h2>概要</h2></div>");
        html.AppendLine("<table><tbody>");
        Row(html, "レポート", report.ReportTitle);
        Row(html, "プロジェクト", report.ProjectName);
        Row(html, "プロジェクトファイル", report.ProjectPath);
        Row(html, "対象", report.ScopeLabel);
        Row(html, "カバレッジ", Percent(report.Summary));
        Row(html, "Statement", $"{report.Summary.CoveredStatements}/{report.Summary.TotalStatements}");
        Row(html, "対象ファイル数", report.Files.Count.ToString(CultureInfo.InvariantCulture));
        html.AppendLine("</tbody></table>");
    }

    private static void RenderRankings(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"rank-grid\">");
        RankingTree(html, "低カバレッジ Namespace", report.Rankings.LowestNamespaces);
        RankingTree(html, "低カバレッジ Type", report.Rankings.LowestTypes);
        RankingMembers(html, "低カバレッジ メンバー", report.Rankings.LowestMembers);
        RankingFiles(html, "未カバーが多いファイル", report.Rankings.MostUncoveredFiles);
        html.AppendLine("</div>");
    }

    private static void RankingTree(StringBuilder html, string title, IReadOnlyList<CoverageTreeItem> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>カバレッジ</th><th>Statement</th><th>名前</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr class=\"coverage-row coverage-").Append(CoverageLevel(item.Summary)).Append("\"><td>");
            CoverageVisual(html, item.Summary);
            html.Append("</td><td>").Append(Statements(item.Summary)).Append("</td><td>")
                .Append(E(item.Name)).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RankingMembers(StringBuilder html, string title, IReadOnlyList<MemberCoverageReport> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>カバレッジ</th><th>Statement</th><th>メンバー</th><th>ファイル</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr class=\"coverage-row coverage-").Append(CoverageLevel(item.Summary)).Append("\"><td>");
            CoverageVisual(html, item.Summary);
            html.Append("</td><td>").Append(Statements(item.Summary)).Append("</td><td>");
            LinkToSource(html, item.FileId, item.StartLine, item.DisplayName);
            html.Append("</td><td>").Append(E(item.RelativePath)).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RankingFiles(StringBuilder html, string title, IReadOnlyList<FileCoverageReport> items)
    {
        html.Append("<section><h2>").Append(E(title)).AppendLine("</h2><table><thead><tr><th>未カバー</th><th>カバレッジ</th><th>ファイル</th></tr></thead><tbody>");
        foreach (var item in items)
        {
            html.Append("<tr class=\"coverage-row coverage-").Append(CoverageLevel(item.Summary)).Append("\"><td>").Append(item.Summary.UncoveredStatements).Append("</td><td>");
            CoverageVisual(html, item.Summary);
            html.Append("</td><td>");
            LinkToSource(html, item.FileId, 1, item.RelativePath);
            html.AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table></section>");
    }

    private static void RenderMembers(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>メンバー</h2><input class=\"filter\" placeholder=\"メンバーを絞り込み\" data-filter-table=\"members-table\"></div>");
        html.AppendLine("<table id=\"members-table\"><thead><tr><th>カバレッジ</th><th>Statement</th><th>ファイル</th><th>Type</th><th>メンバー</th><th>種別</th><th>行</th></tr></thead><tbody>");
        foreach (var member in report.Members)
        {
            html.Append("<tr class=\"coverage-row coverage-").Append(CoverageLevel(member.Summary)).Append("\"><td>");
            CoverageVisual(html, member.Summary);
            html.Append("</td><td>").Append(Statements(member.Summary)).Append("</td><td>")
                .Append(E(member.RelativePath)).Append("</td><td>").Append(E(member.ContainingType)).Append("</td><td>");
            LinkToSource(html, member.FileId, member.StartLine, member.DisplayName);
            html.Append("</td><td>").Append(member.Kind).Append("</td><td>").Append(member.StartLine).Append("-").Append(member.EndLine).AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void RenderFiles(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>ファイル</h2><input class=\"filter\" placeholder=\"ファイルを絞り込み\" data-filter-table=\"files-table\"></div>");
        html.AppendLine("<table id=\"files-table\" class=\"files-table\"><thead><tr><th>カバレッジ</th><th>Statement</th><th>ファイル</th><th>ソース</th></tr></thead><tbody>");
        foreach (var file in report.Files)
        {
            html.Append("<tr class=\"coverage-row coverage-file-row coverage-").Append(CoverageLevel(file.Summary)).Append("\"><td>");
            CoverageVisual(html, file.Summary);
            html.Append("</td><td>").Append(Statements(file.Summary)).Append("</td><td>");
            LinkToSource(html, file.FileId, 1, file.RelativePath);
            html.Append("</td><td>").Append(file.SourceFound ? "あり" : "なし").AppendLine("</td></tr>");
        }

        html.AppendLine("</tbody></table>");
    }

    private static void RenderSource(StringBuilder html, CoverageReport report)
    {
        html.AppendLine("<div class=\"panel-heading\"><h2>ソース</h2><input class=\"filter\" placeholder=\"ソースファイルを絞り込み\" data-filter-details=\"source\"></div>");
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
                html.AppendLine("<tr><td colspan=\"3\">ソースファイルが見つかりません</td></tr>");
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
        return $"{summary.CoveragePercent:0.#}%";
    }

    private static string Statements(CoverageSummary summary)
    {
        return $"{summary.CoveredStatements}/{summary.TotalStatements}";
    }

    private static void CoverageVisual(StringBuilder html, CoverageSummary summary)
    {
        var percent = Percent(summary);
        html.Append("<div class=\"coverage-visual coverage-").Append(CoverageLevel(summary)).Append("\" aria-label=\"カバレッジ ").Append(percent).Append("\">")
            .Append("<div class=\"coverage-bar\"><span style=\"width:").Append(CoverageWidth(summary)).Append("%\"></span></div>")
            .Append("<strong>").Append(percent).Append("</strong></div>");
    }

    private static string CoverageWidth(CoverageSummary summary)
    {
        return Math.Clamp(summary.CoveragePercent, 0m, 100m).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string CoverageLevel(CoverageSummary summary)
    {
        if (summary.TotalStatements == 0)
        {
            return "none";
        }

        return summary.CoveragePercent switch
        {
            >= 80m => "good",
            >= 50m => "warn",
            _ => "bad"
        };
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
        :root { color-scheme: light; --ok:#d7f4dd; --bad:#ffdcdc; --muted:#f5f7fa; --ink:#1f2937; --line:#d7dde5; --accent:#2563eb; --covered:#ABD98D; --empty:#E4E8EB; --warn:#f0c66a; --danger:#df7d7d; --none:#b8c0ca; }
        body { margin:0; color:var(--ink); font-family:Segoe UI, Arial, sans-serif; background:#ffffff; }
        .page-header { padding:20px 24px 12px; border-bottom:1px solid var(--line); background:#f9fafb; }
        h1 { margin:0 0 6px; font-size:24px; letter-spacing:0; }
        h2 { margin:0 0 10px; font-size:18px; letter-spacing:0; }
        .meta { color:#536171; font-size:13px; }
        .summary-grid { display:grid; grid-template-columns:repeat(4,minmax(120px,1fr)); gap:12px; margin-top:16px; }
        .summary-card { background:#fff; border:1px solid var(--line); border-radius:8px; padding:12px; }
        .summary-card span { display:block; font-size:12px; color:#536171; }
        .summary-card strong { display:block; font-size:22px; margin-top:3px; }
        .summary-coverage .coverage-visual { margin-top:8px; }
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
        .coverage-visual { display:grid; grid-template-columns:minmax(96px,1fr) 52px; align-items:center; gap:8px; min-width:160px; }
        .coverage-visual strong { margin:0; font-size:13px; text-align:right; font-variant-numeric:tabular-nums; }
        .coverage-bar { height:12px; overflow:hidden; border:1px solid #cbd2da; border-radius:3px; background:var(--empty); box-shadow:inset 0 1px 0 rgba(255,255,255,.65); }
        .coverage-bar span { display:block; height:100%; background:var(--covered); }
        .coverage-warn .coverage-bar span { background:var(--warn); }
        .coverage-bad .coverage-bar span { background:var(--danger); }
        .coverage-none .coverage-bar span { background:var(--none); }
        .coverage-row.coverage-good td:first-child { box-shadow:inset 4px 0 0 var(--covered); }
        .coverage-row.coverage-warn td:first-child { box-shadow:inset 4px 0 0 var(--warn); }
        .coverage-row.coverage-bad td:first-child { box-shadow:inset 4px 0 0 var(--danger); }
        .coverage-row.coverage-none td:first-child { box-shadow:inset 4px 0 0 var(--none); }
        .files-table th:first-child, .files-table td:first-child { width:230px; }
        .files-table tbody tr:hover { background:#fbfdff; }
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
