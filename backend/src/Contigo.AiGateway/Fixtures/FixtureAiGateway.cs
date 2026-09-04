using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Contigo.AiGateway.Configuration;
using Contigo.AiGateway.Contracts;
using Contigo.SharedKernel;

namespace Contigo.AiGateway.Fixtures;

/// <summary>
/// Deterministic, provider-free <see cref="IAiGateway"/> implementation. No Foundry account or
/// Document Intelligence endpoint exists in this environment yet — there is no
/// <c>infra/modules</c> Terraform module for Azure AI services, and no Foundry connection string
/// anywhere in <c>appsettings*.json</c> — so a live-provider implementation would have nothing to
/// call. ADR-004 "Implications for the decomposition" explicitly allows this: "until then a
/// fixture gateway adapter satisfies R0 scaffolding" (echoed for the `ocr` role by ADR-017, and
/// for Benchmark Service by the module-map: "fixture adapter is enough for first demo").
///
/// This fixture still exercises the full contract a real implementation must honour: config
/// -selected model ids flow into every <see cref="AiCallMetadata"/> (AC-1), classification
/// returns a type and a confidence (AC-2), and every call computes
/// model/version/prompt-version/timestamp/input-hash itself rather than leaving it to the caller
/// (ADR-011). A later task swaps this for a real Foundry-backed implementation behind the same
/// <see cref="IAiGateway"/> seam — domain code never notices (AC-3: "Domain code calls only
/// IAiGateway").
/// </summary>
public sealed class FixtureAiGateway(
    AiGatewayModelOptions modelOptions, IClock clock, AiGatewayOcrOptions? ocrOptions = null) : IAiGateway
{
    /// <summary>
    /// Not a real Foundry prompt version — there is no live prompt behind this fixture. Recorded
    /// so every <see cref="AiCallMetadata"/> is fully populated and callers/tests can assert on
    /// it, per ADR-011's reproducibility fields.
    /// </summary>
    private const string PromptVersion = "fixture-v1";

    /// <summary>
    /// Optional trailing constructor parameter (default <see langword="null"/>, resolved to
    /// <see cref="AiGatewayOcrOptions"/>'s own defaults below) so every existing call site that
    /// constructs this type with the original two arguments — every test written before task
    /// E02/F01/US02/T02, plus <see cref="ServiceCollectionExtensions.AddAiGatewayModule"/>'s own
    /// registration, which resolves this via DI (a container matches this parameter to the
    /// <see cref="AiGatewayOcrOptions"/> singleton that same method now also registers) — keeps
    /// compiling unchanged.
    /// </summary>
    private readonly AiGatewayOcrOptions _ocrOptions = ocrOptions ?? new AiGatewayOcrOptions();

    /// <summary>
    /// Placeholder text for a page this fixture cannot decode (genuine binary content — a real
    /// scanned image/PDF). Named so a test/log line can recognize "this is the fixture's honest
    /// placeholder", not a corrupted real extraction (mirrors <see cref="ExtractAsync"/>'s own
    /// "{}" placeholder — see that method's doc comment).
    /// </summary>
    private const string BinaryContentPlaceholder =
        "[fixture-ocr: {0} bytes of binary content; no live Document Intelligence endpoint configured]";

    /// <summary>
    /// Ordered, case-insensitive substring cues the fixture uses to pick a
    /// <see cref="AiDocumentType"/> deterministically. Checked in order; the first match wins.
    /// Not a real classifier — a real Foundry model replaces this entirely, prompted against the
    /// full <see cref="AiDocumentType"/> taxonomy (ADR-004 candidate: "Small instruction model...
    /// classification is low-complexity").
    /// </summary>
    private static readonly (AiDocumentType Type, string Keyword)[] ClassificationKeywords =
    [
        (AiDocumentType.Msa, "MASTER SERVICES AGREEMENT"),
        (AiDocumentType.Msa, "MSA"),
        (AiDocumentType.OrderForm, "ORDER FORM"),
        (AiDocumentType.Sow, "STATEMENT OF WORK"),
        (AiDocumentType.Sow, "SOW"),
        (AiDocumentType.Amendment, "AMENDMENT"),
        (AiDocumentType.Quote, "QUOTE"),
        (AiDocumentType.Invoice, "INVOICE"),
        (AiDocumentType.PriceList, "PRICE LIST"),
        (AiDocumentType.Nda, "NON-DISCLOSURE AGREEMENT"),
        (AiDocumentType.Nda, "NDA"),
        (AiDocumentType.Dpa, "DATA PROCESSING AGREEMENT"),
        (AiDocumentType.Dpa, "DPA"),
    ];

    /// <inheritdoc/>
    public Task<Result<AiClassificationResult>> ClassifyAsync(
        AiClassificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentText))
        {
            return Task.FromResult(
                Result<AiClassificationResult>.Failure("Classification requires non-empty document text."));
        }

        var upperText = request.DocumentText.ToUpperInvariant();

        var documentType = AiDocumentType.Other;
        var confidence = 0.5;

        foreach (var (type, keyword) in ClassificationKeywords)
        {
            if (upperText.Contains(keyword, StringComparison.Ordinal))
            {
                documentType = type;
                confidence = 0.99;
                break;
            }
        }

        var result = new AiClassificationResult(
            documentType,
            confidence,
            BuildMetadata(modelOptions.Classify, request.DocumentText));

        return Task.FromResult(Result<AiClassificationResult>.Success(result));
    }

    /// <inheritdoc/>
    public Task<Result<AiExtractionResult>> ExtractAsync(
        AiExtractionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentText))
        {
            return Task.FromResult(
                Result<AiExtractionResult>.Failure("Extraction requires non-empty document text."));
        }

        if (string.IsNullOrWhiteSpace(request.JsonSchema))
        {
            return Task.FromResult(Result<AiExtractionResult>.Failure(
                "Extraction requires a target JSON schema (spec §7.3: schema-constrained output)."));
        }

        // No live structured-output model behind this fixture yet. An empty JSON object is a
        // deliberately honest placeholder: "extraction ran, produced nothing to review" rather
        // than fabricating plausible-looking commercial terms the way a naive stub might.
        const string emptyPayload = "{}";

        var result = new AiExtractionResult(
            emptyPayload,
            BuildMetadata(modelOptions.Extract, request.StageName + " " + request.DocumentText + " " + request.JsonSchema));

        return Task.FromResult(Result<AiExtractionResult>.Success(result));
    }

    /// <inheritdoc/>
    public Task<Result<AiEmbeddingResult>> EmbedAsync(
        AiEmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Task.FromResult(Result<AiEmbeddingResult>.Failure("Embedding requires non-empty text."));
        }

        var vector = DeterministicPseudoEmbedding(request.Text);

        var result = new AiEmbeddingResult(
            vector,
            BuildMetadata(modelOptions.Embed, request.Text));

        return Task.FromResult(Result<AiEmbeddingResult>.Success(result));
    }

    /// <inheritdoc/>
    public Task<Result<AiAnswerResult>> AnswerAsync(
        AiAnswerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Task.FromResult(Result<AiAnswerResult>.Failure("A question is required."));
        }

        if (request.Evidence.Count == 0)
        {
            // Appendix C rule 10 / ADR-004: abstain rather than fabricate. ADR-011 puts
            // authorization + retrieval upstream of the gateway, so an empty evidence list means
            // authorized retrieval genuinely found nothing — "cannot determine" is the only
            // honest response, not a failure.
            var abstained = new AiAnswerResult(
                CanDetermine: false,
                Answer: null,
                Citations: [],
                Metadata: BuildMetadata(modelOptions.Answer, request.Question));

            return Task.FromResult(Result<AiAnswerResult>.Success(abstained));
        }

        var citations = request.Evidence
            .Select(evidence => new AiCitation(evidence.DocumentId, evidence.Page, evidence.Section))
            .ToList();

        // No live grounded-generation model behind this fixture yet. The evidence text is
        // surfaced verbatim rather than paraphrased — never say more than the (test) evidence
        // actually contains.
        var answerText = string.Join(" ", request.Evidence.Select(evidence => evidence.Text));

        var grounded = new AiAnswerResult(
            CanDetermine: true,
            Answer: answerText,
            Citations: citations,
            Metadata: BuildMetadata(modelOptions.Answer, request.Question + " " + answerText));

        return Task.FromResult(Result<AiAnswerResult>.Success(grounded));
    }

    /// <inheritdoc/>
    public Task<Result<AiOcrResult>> OcrAsync(
        AiOcrRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Content.IsEmpty)
        {
            return Task.FromResult(Result<AiOcrResult>.Failure("Ocr requires non-empty document content."));
        }

        var pages = DecodePages(request.Content.Span);

        // ADR-017: "over-budget jobs fail visibly (failed status), they are not silently
        // truncated" — checked here, inside the one role every OCR call flows through
        // (AiGatewayOcrOptions's own doc comment: "single choke point"), so no caller can bypass
        // it. A real (non-fixture) implementation would run this same check against the page
        // count Document Intelligence actually reports.
        if (pages.Count > _ocrOptions.MaxPagesPerDocument)
        {
            return Task.FromResult(Result<AiOcrResult>.Failure(
                $"OCR page budget exceeded: document '{request.FileName}' has {pages.Count} pages, " +
                $"configured maximum is {_ocrOptions.MaxPagesPerDocument} (ADR-017: fail visibly, " +
                "never silently truncate)."));
        }

        var result = new AiOcrResult(pages, BuildMetadata(modelOptions.Ocr, request.Content.Span));

        return Task.FromResult(Result<AiOcrResult>.Success(result));
    }

    /// <summary>
    /// No live Document Intelligence endpoint behind this fixture yet (ADR-017 "Implications for
    /// the decomposition": "Fixture OCR is allowed for R0" — the same rule ADR-004 states for the
    /// other four roles, extended by ADR-017 to this one). Deterministically decodes
    /// <paramref name="content"/> as UTF-8 text and splits it on the form-feed character
    /// (<c>\f</c>, U+000C) — a conventional plain-text page-break marker — so tests/callers can
    /// exercise multi-page OCR output without a real scanned-image parser. Content that is not
    /// valid UTF-8 text (genuine binary — a real scanned image/PDF) still returns one honest
    /// placeholder page rather than fabricating plausible-looking contract text, the same "empty
    /// JSON is honest" choice <see cref="ExtractAsync"/> already makes for its own placeholder.
    /// </summary>
    private static IReadOnlyList<AiOcrPage> DecodePages(ReadOnlySpan<byte> content)
    {
        string decoded;
        try
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return [new AiOcrPage(1, string.Format(CultureInfo.InvariantCulture, BinaryContentPlaceholder, content.Length))];
        }

        var pageTexts = decoded.Split('\f');
        var pages = new List<AiOcrPage>(pageTexts.Length);

        for (var i = 0; i < pageTexts.Length; i++)
        {
            pages.Add(new AiOcrPage(i + 1, pageTexts[i]));
        }

        return pages;
    }

    private AiCallMetadata BuildMetadata(AiModelSelection model, string input)
    {
        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        return new AiCallMetadata(model.ModelId, model.ModelVersion, PromptVersion, clock.UtcNow, inputHash);
    }

    /// <summary>Byte-input twin of <see cref="BuildMetadata(AiModelSelection, string)"/> — the
    /// `ocr` role's input is already bytes (see <see cref="AiOcrRequest.Content"/>'s own doc
    /// comment), so hashing it directly avoids a lossy/re-encoding round trip through
    /// <see cref="string"/> for content that may not even be valid text.</summary>
    private AiCallMetadata BuildMetadata(AiModelSelection model, ReadOnlySpan<byte> input)
    {
        var inputHash = Convert.ToHexString(SHA256.HashData(input));
        return new AiCallMetadata(model.ModelId, model.ModelVersion, PromptVersion, clock.UtcNow, inputHash);
    }

    /// <summary>
    /// Deterministic, seedless pseudo-embedding: the same input text always yields the same
    /// vector, and different text yields a (practically certain to be) different one — enough for
    /// fixture-level tests around dimension and determinism without a live embedding model.
    /// Dimension fixed at <see cref="AiGatewayConstants.EmbeddingDimensions"/> to match
    /// <c>Contigo.Documents.Contracts.Domain.Embedding.VectorDimensions</c> (ADR-004: "dimension
    /// fixed at schema time... text-embedding-3-small").
    /// </summary>
    private static float[] DeterministicPseudoEmbedding(string text)
    {
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var vector = new float[AiGatewayConstants.EmbeddingDimensions];

        for (var i = 0; i < vector.Length; i++)
        {
            var b = seedBytes[i % seedBytes.Length];

            // Map a byte (0-255) into [-1, 1], the range typical of normalized embeddings, so
            // consumers exercising vector math (e.g. cosine similarity) get sane fixture inputs.
            vector[i] = (b / 255f * 2f) - 1f;
        }

        return vector;
    }
}
