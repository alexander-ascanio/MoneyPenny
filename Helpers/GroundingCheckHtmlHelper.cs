using System.Globalization;
using System.Net;
using System.Text;
using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Helpers;

public static class GroundingCheckHtmlHelper
{
    public static ResponseGroundingReportViewModel EnrichWithCommentHtml(ResponseGroundingReportViewModel report) =>
        new()
        {
            Verdict = report.Verdict,
            Score = report.Score,
            VerdictLabel = report.VerdictLabel,
            BannerMessage = report.BannerMessage,
            Checks = report.Checks,
            UnsupportedClaims = report.UnsupportedClaims,
            OrphanEntities = report.OrphanEntities,
            InvalidTicketCitations = report.InvalidTicketCitations,
            EvidenceSummary = report.EvidenceSummary,
            Html = ToTeamSupportCommentHtml(report)
        };

    public static string ToTeamSupportCommentHtml(ResponseGroundingReportViewModel model)
    {
        var verdictBadge = model.Verdict switch
        {
            ResponseGroundingVerdict.Pass => ("#16a34a", "#ffffff"),
            ResponseGroundingVerdict.Warn => ("#ca8a04", "#ffffff"),
            _ => ("#dc2626", "#ffffff")
        };

        var bannerStyle = model.Verdict switch
        {
            ResponseGroundingVerdict.Pass => "background:#f0fdf4;color:#166534;",
            ResponseGroundingVerdict.Warn => "background:#fffbeb;color:#92400e;",
            _ => "background:#fef2f2;color:#991b1b;"
        };

        var html = new StringBuilder();
        html.Append("""
            <div style="border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b;max-width:720px;">
            """);

        html.Append("""
            <div style="background:#e2e8f0;color:#334155;font-weight:600;padding:12px 16px;border-bottom:1px solid #cbd5e1;display:flex;flex-wrap:wrap;justify-content:space-between;align-items:center;gap:8px;">
            <span>&#128737; Comprobaci&oacute;n t&eacute;cnica (sin coste)</span>
            <span style="display:inline-flex;align-items:center;gap:8px;">
            """);

        html.Append(CultureInfo.InvariantCulture,
            $"""<span style="display:inline-block;padding:4px 10px;border-radius:999px;background:{verdictBadge.Item1};color:{verdictBadge.Item2};font-size:12px;font-weight:600;">{Encode(model.VerdictLabel)}</span>""");

        html.Append(CultureInfo.InvariantCulture,
            $"""<span style="display:inline-block;padding:4px 10px;border-radius:999px;background:#f1f5f9;color:#1e293b;font-size:12px;font-weight:600;">{model.Score}/100</span>""");

        html.Append("</span></div>");

        html.Append("<div style=\"padding:16px;\">");

        html.Append(CultureInfo.InvariantCulture,
            $"""<div style="{bannerStyle}padding:8px 12px;border-radius:6px;font-size:13px;margin-bottom:12px;">{Encode(model.BannerMessage)}</div>""");

        html.Append("<ul style=\"list-style:none;margin:0 0 12px 0;padding:0;font-size:13px;line-height:1.5;\">");
        foreach (var check in model.Checks)
        {
            html.Append("<li style=\"margin-bottom:8px;\">");
            html.Append(StatusIconHtml(check.Status));
            html.Append(CultureInfo.InvariantCulture,
                $""" <strong>{Encode(check.Id)} {Encode(check.Title)}:</strong> {Encode(check.Message)}""");
            html.Append("</li>");
        }

        html.Append("</ul>");

        if (model.UnsupportedClaims.Count > 0
            || model.OrphanEntities.Count > 0
            || model.InvalidTicketCitations.Count > 0)
        {
            var detailsOpen = model.Verdict != ResponseGroundingVerdict.Pass ? " open" : string.Empty;
            html.Append(CultureInfo.InvariantCulture,
                $"""<details style="font-size:13px;margin-bottom:12px;"{detailsOpen}><summary style="color:#64748b;cursor:pointer;margin-bottom:8px;">Ver detalle</summary>""");

            if (model.UnsupportedClaims.Count > 0)
            {
                html.Append("<p style=\"margin:0 0 4px 0;\"><strong>Afirmaciones dudosas:</strong></p><ul style=\"margin:0 0 8px 18px;padding:0;\">");
                foreach (var claim in model.UnsupportedClaims)
                {
                    html.Append(CultureInfo.InvariantCulture,
                        $"""<li style="margin-bottom:4px;">{Encode(claim)}</li>""");
                }

                html.Append("</ul>");
            }

            if (model.OrphanEntities.Count > 0)
            {
                html.Append(CultureInfo.InvariantCulture,
                    $"""<p style="margin:0 0 8px 0;"><strong>Datos t&eacute;cnicos no respaldados:</strong> {Encode(string.Join(", ", model.OrphanEntities))}</p>""");
            }

            if (model.InvalidTicketCitations.Count > 0)
            {
                var citations = string.Join(", ", model.InvalidTicketCitations.Select(number => $"#{number}"));
                html.Append(CultureInfo.InvariantCulture,
                    $"""<p style="margin:0 0 8px 0;"><strong>Citas inv&aacute;lidas:</strong> {Encode(citations)}</p>""");
            }

            html.Append("</details>");
        }

        var evidence = model.EvidenceSummary;
        var similarity = evidence.MaxSimilarity.ToString("P1", CultureInfo.GetCultureInfo("es-ES"));
        html.Append(CultureInfo.InvariantCulture,
            $"""
             <p style="margin:0;color:#64748b;font-size:12px;line-height:1.5;">
             Evidencia analizada: comentario indexado ({evidence.FirstCommentChars:N0} caracteres)
             + {evidence.ContextTicketCount} ticket(s) similar(es).
             Mejor similitud: {Encode(similarity)}.
             S&iacute;ntoma: {evidence.SymptomTermsMatched}/{evidence.SymptomTermsTotal} t&eacute;rminos clave.
             Esta comprobaci&oacute;n no sustituye la revisi&oacute;n del agente.
             </p>
             """);

        html.Append("</div></div>");
        return html.ToString();
    }

    private static string StatusIconHtml(ResponseGroundingVerdict status) => status switch
    {
        ResponseGroundingVerdict.Pass =>
            """<span style="color:#16a34a;font-weight:700;" aria-hidden="true">&#10003;</span>""",
        ResponseGroundingVerdict.Warn =>
            """<span style="color:#ca8a04;font-weight:700;" aria-hidden="true">&#9888;</span>""",
        _ =>
            """<span style="color:#dc2626;font-weight:700;" aria-hidden="true">&#10007;</span>"""
    };

    private static string Encode(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
