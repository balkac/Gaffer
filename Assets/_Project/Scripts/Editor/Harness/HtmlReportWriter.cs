using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// Renders the harness report as a self-contained HTML fragment in the ART_STYLE "matchday
    /// broadcast graphics" identity: night-pitch teal ground, a single magenta accent, condensed
    /// caps, tabular figures, inline SVG charts. Colours come only from the :root token block (no
    /// hard-coded hex elsewhere). Deliberately single-theme — the broadcast world is dark.
    /// </summary>
    public sealed class HtmlReportWriter
    {
        public string Render(HarnessReport report)
        {
            var html = new StringBuilder();
            html.Append("<title>GAFFER - Season Harness</title>\n");
            html.Append("<style>\n").Append(BuildStyle()).Append("</style>\n");
            html.Append("<div class=\"report\">\n");
            AppendHeader(html, report);
            AppendGates(html, report);
            html.Append("<div class=\"grid\">\n");
            AppendGoals(html, report);
            AppendResultSplit(html, report);
            AppendChampions(html, report);
            html.Append("</div>\n");
            AppendTable(html, report);
            AppendFooter(html, report);
            html.Append("</div>\n");
            return html.ToString();
        }

        private static string BuildStyle()
        {
            return
                ":root{--pitch:#0C1B1A;--pitch-raised:#122624;--pitch-line:#1E3733;--chalk:#EAF2EE;" +
                "--muted:#7C938C;--accent:#FF2E7E;--win:#2FD48A;--loss:#FF5A4D;--draw:#E7B84B;color-scheme:dark;background:#0C1B1A;}\n" +
                "*{box-sizing:border-box;}\n" +
                ".report{background:var(--pitch);color:var(--chalk);min-height:100vh;margin:0 auto;" +
                "padding:32px clamp(16px,4vw,56px);max-width:1180px;line-height:1.5;font-variant-numeric:tabular-nums;" +
                "font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',system-ui,Roboto,sans-serif;}\n" +
                ".bug{display:flex;align-items:baseline;gap:18px;flex-wrap:wrap;border-bottom:2px solid var(--accent);padding-bottom:14px;margin-bottom:26px;}\n" +
                ".bug-mark{font-weight:800;letter-spacing:.16em;text-transform:uppercase;font-size:27px;}\n" +
                ".bug-mark b{color:var(--accent);}\n" +
                ".bug-title{color:var(--muted);text-transform:uppercase;letter-spacing:.14em;font-size:12px;font-weight:700;}\n" +
                ".bug-meta{margin-left:auto;color:var(--muted);font-size:12px;letter-spacing:.06em;}\n" +
                ".gates{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:12px;margin-bottom:22px;}\n" +
                ".chip{position:relative;background:var(--pitch-raised);border:1px solid var(--pitch-line);border-left:3px solid var(--muted);border-radius:9px;padding:13px 15px;}\n" +
                ".chip.pass{border-left-color:var(--win);}.chip.warn{border-left-color:var(--draw);}.chip.fail{border-left-color:var(--loss);}\n" +
                ".chip .k{font-size:10px;letter-spacing:.12em;text-transform:uppercase;color:var(--muted);}\n" +
                ".chip .v{font-size:22px;font-weight:800;margin-top:3px;}\n" +
                ".chip .d{font-size:11px;color:var(--muted);margin-top:5px;line-height:1.35;}\n" +
                ".tag{position:absolute;top:13px;right:14px;font-size:9px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;}\n" +
                ".pass .tag{color:var(--win);}.warn .tag{color:var(--draw);}.fail .tag{color:var(--loss);}\n" +
                ".grid{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:16px;}\n" +
                ".card{background:var(--pitch-raised);border:1px solid var(--pitch-line);border-radius:12px;padding:20px 22px;}\n" +
                ".wide{grid-column:1/-1;}\n" +
                ".card h2{font-size:12px;letter-spacing:.12em;text-transform:uppercase;color:var(--muted);margin:0 0 6px;}\n" +
                ".stat{font-size:34px;font-weight:800;margin:0 0 14px;}\n" +
                ".stat small{font-size:13px;color:var(--muted);font-weight:600;letter-spacing:.04em;}\n" +
                "svg{width:100%;height:auto;display:block;}\n" +
                ".legend{display:flex;flex-wrap:wrap;gap:16px;margin-top:12px;font-size:11px;color:var(--muted);letter-spacing:.04em;}\n" +
                ".legend span{display:inline-flex;align-items:center;gap:7px;}\n" +
                ".sw{width:11px;height:11px;border-radius:3px;display:inline-block;}\n" +
                ".split{display:flex;height:28px;border-radius:7px;overflow:hidden;margin-top:6px;border:1px solid var(--pitch-line);}\n" +
                ".split i{display:block;height:100%;}\n" +
                ".sub{margin-top:14px;font-size:12px;color:var(--muted);}\n" +
                ".sub b{color:var(--chalk);font-weight:700;}\n" +
                ".scroll{overflow-x:auto;}\n" +
                "table{width:100%;border-collapse:collapse;font-size:13px;min-width:560px;}\n" +
                "th,td{padding:8px 10px;text-align:right;white-space:nowrap;}\n" +
                "th:nth-child(2),td:nth-child(2){text-align:left;width:100%;}\n" +
                "th{color:var(--muted);font-size:10px;letter-spacing:.1em;text-transform:uppercase;border-bottom:1px solid var(--pitch-line);font-weight:700;}\n" +
                "tbody tr{border-bottom:1px solid rgba(30,55,51,.55);}\n" +
                "td.pos{color:var(--muted);}\n" +
                "td.pts{font-weight:800;}\n" +
                "tr.champ{background:rgba(255,46,126,.09);box-shadow:inset 3px 0 0 var(--accent);}\n" +
                "tr.releg{background:rgba(255,90,77,.06);}\n" +
                ".foot{margin-top:22px;color:var(--muted);font-size:11px;letter-spacing:.04em;}\n" +
                "@media(max-width:760px){.grid{grid-template-columns:1fr;}}\n";
        }

        private static void AppendHeader(StringBuilder html, HarnessReport report)
        {
            HarnessConfig config = report.Config;
            html.Append("<header class=\"bug\">");
            html.Append("<div class=\"bug-mark\">GAFF<b>E</b>R</div>");
            html.Append("<div class=\"bug-title\">Season Harness / Believability Report</div>");
            html.Append("<div class=\"bug-meta\">")
                .Append(config.SeasonCount).Append(" seasons &middot; ")
                .Append(config.TeamCount).Append(" teams &middot; seed ")
                .Append(config.Seed).Append(" &middot; ")
                .Append(report.TotalMatches).Append(" matches</div>");
            html.Append("</header>\n");
        }

        private static void AppendGates(StringBuilder html, HarnessReport report)
        {
            html.Append("<section class=\"gates\">");
            foreach (GateCheck check in report.GateChecks)
            {
                string cls = check.Status == GateStatus.Pass ? "pass" : check.Status == GateStatus.Warn ? "warn" : "fail";
                string tag = check.Status == GateStatus.Pass ? "PASS" : check.Status == GateStatus.Warn ? "WARN" : "FAIL";
                html.Append("<div class=\"chip ").Append(cls).Append("\">");
                html.Append("<span class=\"tag\">").Append(tag).Append("</span>");
                html.Append("<div class=\"k\">").Append(Encode(check.Label)).Append("</div>");
                html.Append("<div class=\"v\">").Append(Encode(check.Value)).Append("</div>");
                html.Append("<div class=\"d\">").Append(Encode(check.Detail)).Append("</div>");
                html.Append("</div>");
            }
            html.Append("</section>\n");
        }

        private static void AppendGoals(StringBuilder html, HarnessReport report)
        {
            html.Append("<section class=\"card\">");
            html.Append("<h2>Goals per match</h2>");
            html.Append("<div class=\"stat\">").Append(Number(report.AverageGoalsPerMatch, "F2"))
                .Append(" <small>average</small></div>");

            var bars = new List<Bar>();
            foreach (HistogramBin bin in report.GoalBins)
            {
                bars.Add(new Bar(bin.Label, bin.Percentage, Number(bin.Percentage, "F1")));
            }
            AppendBarChart(html, bars);
            html.Append("</section>\n");
        }

        private static void AppendResultSplit(StringBuilder html, HarnessReport report)
        {
            html.Append("<section class=\"card\">");
            html.Append("<h2>Result split</h2>");
            html.Append("<div class=\"stat\">").Append(Number(report.HomeWinPercentage, "F1"))
                .Append("% <small>home wins</small></div>");

            html.Append("<div class=\"split\">");
            html.Append("<i style=\"flex:").Append(Number(report.HomeWinPercentage, "F2")).Append(";background:var(--accent);\"></i>");
            html.Append("<i style=\"flex:").Append(Number(report.DrawPercentage, "F2")).Append(";background:rgba(234,242,238,.34);\"></i>");
            html.Append("<i style=\"flex:").Append(Number(report.AwayWinPercentage, "F2")).Append(";background:rgba(234,242,238,.13);\"></i>");
            html.Append("</div>");

            html.Append("<div class=\"legend\">");
            html.Append("<span><i class=\"sw\" style=\"background:var(--accent);\"></i>Home ").Append(Number(report.HomeWinPercentage, "F1")).Append("%</span>");
            html.Append("<span><i class=\"sw\" style=\"background:rgba(234,242,238,.34);\"></i>Draw ").Append(Number(report.DrawPercentage, "F1")).Append("%</span>");
            html.Append("<span><i class=\"sw\" style=\"background:rgba(234,242,238,.13);\"></i>Away ").Append(Number(report.AwayWinPercentage, "F1")).Append("%</span>");
            html.Append("</div>");

            html.Append("<div class=\"sub\">Favourite (stronger side): <b>")
                .Append(Number(report.FavouriteWinPercentage, "F1")).Append("%</b> win &middot; <b>")
                .Append(Number(report.FavouriteDrawPercentage, "F1")).Append("%</b> draw &middot; <b>")
                .Append(Number(report.FavouriteUpsetPercentage, "F1")).Append("%</b> upset</div>");
            html.Append("</section>\n");
        }

        private static void AppendChampions(StringBuilder html, HarnessReport report)
        {
            html.Append("<section class=\"card wide\">");
            html.Append("<h2>Title race &mdash; champions by pre-season seed (1 = strongest)</h2>");

            var bars = new List<Bar>();
            foreach (ChampionShare share in report.ChampionShares)
            {
                bars.Add(new Bar((share.Rank + 1).ToString(), share.Percentage, Number(share.Percentage, "F0")));
            }
            AppendBarChart(html, bars);
            html.Append("</section>\n");
        }

        private static void AppendTable(StringBuilder html, HarnessReport report)
        {
            html.Append("<section class=\"card\">");
            html.Append("<h2>A sample final table (first simulated season)</h2>");
            html.Append("<div class=\"scroll\"><table><thead><tr>");
            string[] heads = { "#", "Club", "P", "W", "D", "L", "GF", "GA", "GD", "Pts" };
            foreach (string head in heads)
            {
                html.Append("<th>").Append(head).Append("</th>");
            }
            html.Append("</tr></thead><tbody>");

            int relegationFrom = report.SampleTable.Count - 3;
            foreach (TableRowView row in report.SampleTable)
            {
                string rowClass = row.Position == 1 ? " class=\"champ\"" : row.Position > relegationFrom ? " class=\"releg\"" : "";
                html.Append("<tr").Append(rowClass).Append(">");
                html.Append("<td class=\"pos\">").Append(row.Position).Append("</td>");
                html.Append("<td>").Append(Encode(row.Name)).Append("</td>");
                html.Append("<td>").Append(row.Played).Append("</td>");
                html.Append("<td>").Append(row.Won).Append("</td>");
                html.Append("<td>").Append(row.Drawn).Append("</td>");
                html.Append("<td>").Append(row.Lost).Append("</td>");
                html.Append("<td>").Append(row.GoalsFor).Append("</td>");
                html.Append("<td>").Append(row.GoalsAgainst).Append("</td>");
                html.Append("<td>").Append(Signed(row.GoalDifference)).Append("</td>");
                html.Append("<td class=\"pts\">").Append(row.Points).Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table></div></section>\n");
        }

        private static void AppendFooter(StringBuilder html, HarnessReport report)
        {
            html.Append("<div class=\"foot\">Headless dotnet console over the pure Application core &middot; ")
                .Append("deterministic (seed ").Append(report.Config.Seed).Append(") &middot; balance untuned (Faz 1 target ~2.5-3.0 goals)</div>\n");
        }

        // --- inline SVG vertical bar chart, accent bars over a faint baseline grid ---

        private sealed class Bar
        {
            public Bar(string label, double value, string valueText)
            {
                Label = label;
                Value = value;
                ValueText = valueText;
            }

            public string Label { get; }

            public double Value { get; }

            public string ValueText { get; }
        }

        private static void AppendBarChart(StringBuilder html, IReadOnlyList<Bar> bars)
        {
            const int step = 46;
            const int barWidth = 28;
            const int plotHeight = 150;
            const int topPad = 20;
            const int labelPad = 26;
            int width = bars.Count * step;
            int height = plotHeight + topPad + labelPad;

            double max = 0.0;
            foreach (Bar bar in bars)
            {
                if (bar.Value > max)
                {
                    max = bar.Value;
                }
            }
            if (max <= 0.0)
            {
                max = 1.0;
            }

            html.Append("<svg viewBox=\"0 0 ").Append(width).Append(' ').Append(height)
                .Append("\" role=\"img\">");

            int baseline = topPad + plotHeight;
            html.Append("<line x1=\"0\" y1=\"").Append(baseline).Append("\" x2=\"").Append(width)
                .Append("\" y2=\"").Append(baseline).Append("\" stroke=\"var(--pitch-line)\" stroke-width=\"1\"/>");

            for (int i = 0; i < bars.Count; i++)
            {
                Bar bar = bars[i];
                int barHeight = (int)(plotHeight * (bar.Value / max));
                if (bar.Value > 0.0 && barHeight < 2)
                {
                    barHeight = 2;
                }
                int x = (i * step) + ((step - barWidth) / 2);
                int y = baseline - barHeight;

                html.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y)
                    .Append("\" width=\"").Append(barWidth).Append("\" height=\"").Append(barHeight)
                    .Append("\" rx=\"3\" fill=\"var(--accent)\"/>");

                if (bar.Value >= 1.0)
                {
                    AppendText(html, x + (barWidth / 2), y - 6, "var(--chalk)", 11, bar.ValueText);
                }
                AppendText(html, x + (barWidth / 2), baseline + 16, "var(--muted)", 11, bar.Label);
            }

            html.Append("</svg>");
        }

        private static void AppendText(StringBuilder html, int x, int y, string fill, int size, string value)
        {
            html.Append("<text x=\"").Append(x).Append("\" y=\"").Append(y)
                .Append("\" fill=\"").Append(fill).Append("\" font-size=\"").Append(size)
                .Append("\" text-anchor=\"middle\" font-family=\"inherit\">").Append(Encode(value)).Append("</text>");
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Number(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string Encode(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
