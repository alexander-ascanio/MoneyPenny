using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MoneyPenny.Options;
using MoneyPenny.ViewModels.Rag;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Validation;

public class ResponseGroundingChecker : IResponseGroundingChecker
{
    private static readonly Regex TicketCitationRegex = new(
        @"#(\d{5,7})\b|ticket\s+#?(\d{5,7})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Ipv4Regex = new(
        @"\b\d{1,3}(?:\.\d{1,3}){3}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UncPathRegex = new(
        @"\\\\[^\s\\]+(?:\\[^\s\\]+)+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CertificateFileRegex = new(
        @"\b[\w\\./:-]+\.(?:p12|pfx)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PortPhraseRegex = new(
        @"\bpuerto\s+(\d{1,5})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] GenericClaimMarkers =
    [
        "si el problema persiste",
        "contacte con soporte",
        "abrir un nuevo ticket",
        "un saludo",
        "quedo a la espera",
        "consulta la guía",
        "consulta la guia",
        "se recomienda abrir"
    ];

    private static readonly string[] StopWords =
    [
        "para", "como", "este", "esta", "estos", "estas", "donde", "cuando", "desde", "hasta",
        "sobre", "bajo", "entre", "pero", "porque", "aunque", "mientras", "tambien", "también",
        "solo", "sólo", "cada", "todo", "toda", "todos", "todas", "puede", "pueden", "debe",
        "deben", "sera", "será", "hace", "hacer", "hecho", "tiene", "tienen", "haber", "hay",
        "esta", "está", "estan", "están", "fue", "fueron", "sido", "siendo", "mismo", "misma",
        "otro", "otra", "otros", "otras", "algo", "algun", "algún", "alguna", "ningun", "ningún",
        "ninguna", "muy", "mas", "más", "menos", "bien", "mal", "aqui", "aquí", "alli", "allí",
        "with", "that", "this", "from", "have", "been", "will", "your", "their", "them", "they",
        "asegurate", "asegúrate", "revisar", "verificar", "comprobar", "siguiente", "siguientes",
        "pasos", "paso", "basados", "basado", "similar", "similares", "casos", "caso", "cliente",
        "ticket", "tickets", "comentario", "descrito", "descrita", "problema", "solucion", "solución",
        "buenos", "dias", "días", "tardes", "adjunto", "adjunta", "foto", "imagen"
    ];

    private static readonly string[] KnownProducts =
    [
        "go!manage", "go manage", "odbc", "openedge", "teamsupport", "efactura", "e-factura",
        "ticketbai", "verifactu", "sii", "tbai"
    ];

    private readonly RagGroundingCheckOptions _options;

    public ResponseGroundingChecker(IOptions<RagOptions> options)
    {
        _options = options.Value.GroundingCheck;
    }

    public ResponseGroundingReportViewModel Evaluate(ResponseGroundingRequest request)
    {
        var corpus = BuildCorpus(request);
        var corpusText = string.Join("\n", corpus);
        var normalizedCorpus = Normalize(corpusText);

        var checks = new List<ResponseGroundingCheckItemViewModel>();
        var unsupportedClaims = new List<string>();
        var orphanEntities = new List<string>();
        var invalidCitations = new List<string>();

        var maxScore = request.ContextItems.Count > 0
            ? request.ContextItems.Max(item => item.Score)
            : 0d;
        var hasContext = request.ContextItems.Count > 0;

        checks.Add(EvaluateRetrieval(hasContext, maxScore));

        var validTicketNumbers = request.ContextItems
            .Select(item => item.TicketNumber)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.TicketNumber))
        {
            validTicketNumbers.Add(request.TicketNumber);
        }

        checks.Add(EvaluateTicketCitations(request.Answer, validTicketNumbers, invalidCitations));

        checks.Add(EvaluateTechnicalEntities(request.Answer, normalizedCorpus, orphanEntities));

        var symptomTerms = ExtractSymptomTerms(request.FirstCommentContent);
        var matchedSymptoms = CountSymptomMatches(request.Answer, symptomTerms);
        checks.Add(EvaluateSymptomCoverage(symptomTerms, matchedSymptoms));

        var claims = ExtractClaims(request.Answer);
        var unsupportedCount = 0;
        foreach (var claim in claims)
        {
            if (IsClaimSupported(claim, corpus, normalizedCorpus))
            {
                continue;
            }

            unsupportedCount++;
            if (unsupportedClaims.Count < 2)
            {
                unsupportedClaims.Add(Truncate(claim, 220));
            }
        }

        checks.Add(EvaluateClaims(unsupportedCount, unsupportedClaims));

        var evidenceLen = (request.FirstCommentContent?.Length ?? 0)
            + request.ContextItems.Sum(item => item.Content.Length)
            + (request.KnowledgeBaseSolutionText?.Length ?? 0);
        checks.Add(EvaluateProportionality(request.Answer.Length, evidenceLen, maxScore));

        var score = checks.Sum(check => check.Points);
        var hasCriticalFail = checks.Any(check =>
            check.Status == ResponseGroundingVerdict.Fail
            && check.Id is "R1" or "R2" or "R3" or "R5");

        var verdict = hasCriticalFail || score < _options.GlobalWarnMinScore
            ? ResponseGroundingVerdict.Fail
            : score >= _options.GlobalPassMinScore
                ? ResponseGroundingVerdict.Pass
                : ResponseGroundingVerdict.Warn;

        return new ResponseGroundingReportViewModel
        {
            Verdict = verdict,
            Score = score,
            VerdictLabel = verdict switch
            {
                ResponseGroundingVerdict.Pass => "Fundamentada",
                ResponseGroundingVerdict.Warn => "Revisar",
                _ => "No fiable"
            },
            BannerMessage = verdict switch
            {
                ResponseGroundingVerdict.Pass =>
                    "La respuesta está alineada con el comentario #1 y el contexto recuperado.",
                ResponseGroundingVerdict.Warn =>
                    "Revisar antes de usar con el cliente: hay avisos en la comprobación técnica.",
                _ => "No usar tal cual: hay datos no respaldados por el contexto disponible."
            },
            Checks = checks,
            UnsupportedClaims = unsupportedClaims,
            OrphanEntities = orphanEntities,
            InvalidTicketCitations = invalidCitations,
            EvidenceSummary = new ResponseGroundingEvidenceSummaryViewModel
            {
                FirstCommentChars = request.FirstCommentContent?.Length ?? 0,
                ContextTicketCount = request.ContextItems.Count,
                MaxSimilarity = maxScore,
                SymptomTermsMatched = matchedSymptoms,
                SymptomTermsTotal = symptomTerms.Count
            }
        };
    }

    private ResponseGroundingCheckItemViewModel EvaluateRetrieval(bool hasContext, double maxScore)
    {
        if (!hasContext || maxScore < _options.RetrievalWarnMinScore)
        {
            return Check(
                "R1",
                ResponseGroundingVerdict.Fail,
                0,
                "Calidad del contexto",
                hasContext
                    ? $"Contexto insuficiente (mejor similitud {maxScore:P1})."
                    : "No hay tickets similares en el contexto recuperado.");
        }

        if (maxScore < _options.RetrievalPassMinScore)
        {
            return Check(
                "R1",
                ResponseGroundingVerdict.Warn,
                10,
                "Calidad del contexto",
                $"Contexto limitado (mejor similitud {maxScore:P1}). Revisa los tickets listados.");
        }

        return Check(
            "R1",
            ResponseGroundingVerdict.Pass,
            20,
            "Calidad del contexto",
            $"Contexto adecuado (mejor similitud {maxScore:P1}).");
    }

    private ResponseGroundingCheckItemViewModel EvaluateTicketCitations(
        string answer,
        HashSet<string> validTicketNumbers,
        List<string> invalidCitations)
    {
        var cited = ExtractCitedTicketNumbers(answer);
        if (cited.Count == 0)
        {
            return Check(
                "R2",
                ResponseGroundingVerdict.Pass,
                15,
                "Citas de tickets",
                "No se detectaron citas de tickets (adecuado para respuestas dirigidas al cliente).");
        }

        foreach (var number in cited)
        {
            if (!validTicketNumbers.Contains(number))
            {
                invalidCitations.Add(number);
            }
        }

        if (invalidCitations.Count > 0)
        {
            return Check(
                "R2",
                ResponseGroundingVerdict.Fail,
                0,
                "Citas de tickets",
                $"Cita ticket(s) no presentes en el contexto: {string.Join(", ", invalidCitations.Select(n => $"#{n}"))}.");
        }

        return Check(
            "R2",
            ResponseGroundingVerdict.Pass,
            15,
            "Citas de tickets",
            "Todas las citas corresponden al ticket actual o al contexto recuperado.");
    }

    private ResponseGroundingCheckItemViewModel EvaluateTechnicalEntities(
        string answer,
        string normalizedCorpus,
        List<string> orphanEntities)
    {
        foreach (var entity in ExtractTechnicalEntities(answer))
        {
            if (!EntityPresentInCorpus(entity, normalizedCorpus))
            {
                orphanEntities.Add(entity);
            }
        }

        if (orphanEntities.Count >= 2
            || orphanEntities.Any(entity => Ipv4Regex.IsMatch(entity) || entity.Contains("puerto", StringComparison.OrdinalIgnoreCase)))
        {
            return Check(
                "R3",
                ResponseGroundingVerdict.Fail,
                0,
                "Entidades técnicas",
                $"Datos técnicos sin respaldo: {string.Join(", ", orphanEntities.Take(3))}.");
        }

        if (orphanEntities.Count == 1)
        {
            return Check(
                "R3",
                ResponseGroundingVerdict.Warn,
                10,
                "Entidades técnicas",
                $"Dato técnico no encontrado en el contexto: {orphanEntities[0]}.");
        }

        return Check(
            "R3",
            ResponseGroundingVerdict.Pass,
            20,
            "Entidades técnicas",
            "Los datos técnicos citados aparecen en el comentario o en el contexto.");
    }

    private ResponseGroundingCheckItemViewModel EvaluateSymptomCoverage(
        IReadOnlyList<string> symptomTerms,
        int matchedSymptoms)
    {
        if (symptomTerms.Count == 0)
        {
            return Check(
                "R4",
                ResponseGroundingVerdict.Pass,
                15,
                "Cobertura del síntoma",
                "No se detectaron términos clave en el comentario #1.");
        }

        var ratio = matchedSymptoms / (double)symptomTerms.Count;
        if (ratio >= _options.SymptomCoveragePass || matchedSymptoms >= 1 && symptomTerms.Count <= 2)
        {
            return Check(
                "R4",
                ResponseGroundingVerdict.Pass,
                15,
                "Cobertura del síntoma",
                $"La respuesta aborda el problema del comentario ({matchedSymptoms}/{symptomTerms.Count} términos clave).");
        }

        if (matchedSymptoms > 0)
        {
            return Check(
                "R4",
                ResponseGroundingVerdict.Warn,
                8,
                "Cobertura del síntoma",
                $"Cobertura parcial del síntoma ({matchedSymptoms}/{symptomTerms.Count} términos clave).");
        }

        return Check(
            "R4",
            ResponseGroundingVerdict.Fail,
            0,
            "Cobertura del síntoma",
            "La respuesta no menciona el síntoma principal del comentario #1.");
    }

    private ResponseGroundingCheckItemViewModel EvaluateClaims(
        int unsupportedCount,
        IReadOnlyList<string> unsupportedClaims)
    {
        if (unsupportedCount >= 2)
        {
            return Check(
                "R5",
                ResponseGroundingVerdict.Fail,
                0,
                "Afirmaciones respaldadas",
                $"{unsupportedCount} afirmación(es) sin respaldo claro en el contexto.");
        }

        if (unsupportedCount == 1)
        {
            var detail = unsupportedClaims.FirstOrDefault() is { Length: > 0 } claim
                ? Truncate(claim, 120)
                : "una afirmación";
            return Check(
                "R5",
                ResponseGroundingVerdict.Warn,
                10,
                "Afirmaciones respaldadas",
                $"Una afirmación sin respaldo claro: «{detail}».");
        }

        return Check(
            "R5",
            ResponseGroundingVerdict.Pass,
            20,
            "Afirmaciones respaldadas",
            "Todas las afirmaciones evaluadas están respaldadas por el contexto.");
    }

    private ResponseGroundingCheckItemViewModel EvaluateProportionality(
        int answerLen,
        int evidenceLen,
        double maxScore)
    {
        if (evidenceLen <= 0)
        {
            return Check(
                "R6",
                ResponseGroundingVerdict.Warn,
                5,
                "Proporcionalidad",
                "No hay evidencia suficiente para comparar la longitud de la respuesta.");
        }

        var ratio = answerLen / (double)Math.Max(evidenceLen, 1);
        if (ratio > 2.5 && maxScore < _options.RetrievalWarnMinScore)
        {
            return Check(
                "R6",
                ResponseGroundingVerdict.Fail,
                0,
                "Proporcionalidad",
                "Respuesta muy extensa respecto al contexto disponible.");
        }

        if (ratio > 1.5 && maxScore < _options.RetrievalPassMinScore)
        {
            return Check(
                "R6",
                ResponseGroundingVerdict.Warn,
                5,
                "Proporcionalidad",
                "Respuesta extensa respecto al contexto; conviene revisión manual.");
        }

        return Check(
            "R6",
            ResponseGroundingVerdict.Pass,
            10,
            "Proporcionalidad",
            "Longitud de la respuesta proporcional al contexto.");
    }

    private static ResponseGroundingCheckItemViewModel Check(
        string id,
        ResponseGroundingVerdict status,
        int points,
        string title,
        string message) =>
        new()
        {
            Id = id,
            Status = status,
            Points = points,
            Title = title,
            Message = message
        };

    private static List<string> BuildCorpus(ResponseGroundingRequest request)
    {
        var corpus = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.FirstCommentContent))
        {
            corpus.AddRange(SplitParagraphs(request.FirstCommentContent));
        }

        foreach (var item in request.ContextItems)
        {
            corpus.AddRange(SplitParagraphs(item.Content));
        }

        if (!string.IsNullOrWhiteSpace(request.KnowledgeBaseSolutionText))
        {
            corpus.AddRange(SplitParagraphs(request.KnowledgeBaseSolutionText));
        }

        return corpus.Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)).ToList();
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length >= 20);

    private static List<string> ExtractClaims(string answer)
    {
        var claims = new List<string>();
        foreach (var rawLine in answer.Split('\n', StringSplitOptions.TrimEntries))
        {
            var line = Regex.Replace(rawLine, @"^\s*(?:[-*•]|\d+\.)\s+", string.Empty).Trim();
            if (line.Length < 25 || IsGenericClaim(line))
            {
                continue;
            }

            claims.Add(line);

            foreach (var sentence in SplitSentences(line))
            {
                if (sentence.Length >= 25 && !IsGenericClaim(sentence))
                {
                    claims.Add(sentence);
                }
            }
        }

        return claims
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool IsClaimSupported(string claim, IReadOnlyList<string> corpus, string normalizedCorpus)
    {
        var claimTokens = Tokenize(claim);
        if (claimTokens.Count == 0)
        {
            return true;
        }

        var bestJaccard = 0d;
        foreach (var paragraph in corpus)
        {
            var paragraphTokens = Tokenize(paragraph);
            if (paragraphTokens.Count == 0)
            {
                continue;
            }

            var intersection = claimTokens.Intersect(paragraphTokens).Count();
            var union = claimTokens.Union(paragraphTokens).Count();
            if (union == 0)
            {
                continue;
            }

            bestJaccard = Math.Max(bestJaccard, intersection / (double)union);
        }

        if (bestJaccard >= _options.ClaimJaccardPass)
        {
            return true;
        }

        var technicalTokens = ExtractTechnicalTokens(claim);
        if (technicalTokens.Count == 0)
        {
            return bestJaccard >= _options.ClaimJaccardPass * 0.75;
        }

        var covered = technicalTokens.Count(token => normalizedCorpus.Contains(Normalize(token), StringComparison.Ordinal));
        return covered / (double)technicalTokens.Count >= _options.ClaimTokenCoveragePass;
    }

    private static HashSet<string> ExtractCitedTicketNumbers(string answer)
    {
        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in TicketCitationRegex.Matches(answer))
        {
            var number = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    private static List<string> ExtractTechnicalEntities(string answer)
    {
        var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Ipv4Regex.Matches(answer))
        {
            entities.Add(match.Value);
        }

        foreach (Match match in UncPathRegex.Matches(answer))
        {
            entities.Add(match.Value);
        }

        foreach (Match match in CertificateFileRegex.Matches(answer))
        {
            entities.Add(match.Value);
        }

        foreach (Match match in PortPhraseRegex.Matches(answer))
        {
            entities.Add($"puerto {match.Groups[1].Value}");
        }

        foreach (var product in KnownProducts)
        {
            if (answer.Contains(product, StringComparison.OrdinalIgnoreCase))
            {
                entities.Add(product);
            }
        }

        foreach (Match match in Regex.Matches(answer, @"""(\d{4,8})"""))
        {
            entities.Add(match.Groups[1].Value);
        }

        return entities.ToList();
    }

    private static bool EntityPresentInCorpus(string entity, string normalizedCorpus)
    {
        var normalizedEntity = Normalize(entity);
        if (normalizedCorpus.Contains(normalizedEntity, StringComparison.Ordinal))
        {
            return true;
        }

        if (entity.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && normalizedCorpus.Contains("127.0.0.1", StringComparison.Ordinal))
        {
            return true;
        }

        if (entity.Contains("go manage", StringComparison.OrdinalIgnoreCase)
            && normalizedCorpus.Contains("go!manage", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static List<string> ExtractSymptomTerms(string? firstCommentContent)
    {
        if (string.IsNullOrWhiteSpace(firstCommentContent))
        {
            return [];
        }

        return Tokenize(firstCommentContent)
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static int CountSymptomMatches(string answer, IReadOnlyList<string> symptomTerms)
    {
        if (symptomTerms.Count == 0)
        {
            return 0;
        }

        var normalizedAnswer = Normalize(answer);
        return symptomTerms.Count(term =>
            normalizedAnswer.Contains(Normalize(term), StringComparison.Ordinal));
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(Normalize(text), @"[\p{L}\p{N}]{4,}"))
        {
            var token = match.Value;
            if (StopWords.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }

    private static HashSet<string> ExtractTechnicalTokens(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in ExtractTechnicalEntities(text))
        {
            tokens.Add(Normalize(entity));
        }

        foreach (Match match in Regex.Matches(text, @"\b\d{4,8}\b"))
        {
            tokens.Add(match.Value);
        }

        return tokens;
    }

    private static IEnumerable<string> SplitSentences(string text) =>
        text.Split(['.', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsGenericClaim(string text) =>
        GenericClaimMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
