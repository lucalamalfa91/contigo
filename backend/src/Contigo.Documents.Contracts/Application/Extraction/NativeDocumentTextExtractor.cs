using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Contigo.Documents.Contracts.Application.Extraction;

/// <summary>
/// Real (non-AI) text extraction for the three born-digital formats spec §4 accepts — PDF, DOCX,
/// XLSX — added by task E02/F01/US02/T02 (hybrid-ocr) so <see cref="HybridDocumentParsingService"/>
/// has a genuine "is this actually native/sufficient" signal for PDF (the one format that can be
/// either born-digital or scanned) rather than always falling back to the pay-per-page `ocr`
/// gateway role — that would technically satisfy "hybrid" in name only, defeating ADR-017's own
/// "native text extraction... keeps born-digital cost low".
///
/// <b>DOCX/XLSX</b>: read via the real <c>DocumentFormat.OpenXml</c> SDK (OOXML is just XML in a
/// zip archive — not a provider SDK, so this stays clear of ADR-002's "no provider SDK in domain
/// code" rule the same way <c>EFCore.NamingConventions</c>/<c>Npgsql</c> already do for this
/// project). ADR-017's own assumption ("native libraries for PDF/DOCX/XLSX... good enough for
/// born-digital files") holds unconditionally for these two: a Word/Excel file is never a scanned
/// image, so a successful parse is always <see cref="NativeTextExtractionResult.IsSufficient"/>,
/// regardless of how much text it actually contains — an empty document is an honest fact, not a
/// reason to spend an OCR page on it.
///
/// <b>PDF</b> is genuinely hybrid: born-digital PDFs and scanned/image PDFs share the same mime
/// type. This class does <em>not</em> depend on a third-party PDF library — at implementation
/// time the obvious choice (<c>UglyToad.PdfPig</c>) had no stable release on the package feed
/// (only an unofficial "-custom-" prerelease tag), which is a worse supply-chain risk than a
/// narrowly-scoped, self-contained reader. <see cref="ExtractPdfNatively"/>'s own doc comment
/// spells out exactly what this lightweight scan does and does not handle; when it cannot be
/// confident, it reports <see cref="NativeTextExtractionResult.IsSufficient"/> = <see
/// langword="false"/> and lets the `ocr` gateway role (backed by real Document Intelligence once
/// wired) take over — the safe direction to be wrong in (ADR-017: "OCR is the backstop, not a
/// replacement" — never the other way around).
/// </summary>
public sealed class NativeDocumentTextExtractor : INativeDocumentTextExtractor
{
    private const string PdfMimeType = "application/pdf";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc/>
    public bool CanHandle(string mimeType) => Normalize(mimeType) is PdfMimeType or DocxMimeType or XlsxMimeType;

    /// <inheritdoc/>
    public NativeTextExtractionResult Extract(string mimeType, ReadOnlyMemory<byte> content) =>
        Normalize(mimeType) switch
        {
            PdfMimeType => ExtractPdfNatively(content),
            DocxMimeType => ExtractDocx(content),
            XlsxMimeType => ExtractXlsx(content),
            _ => throw new NotSupportedException(
                $"{nameof(NativeDocumentTextExtractor)} cannot handle mime type '{mimeType}'; " +
                $"call {nameof(CanHandle)} first."),
        };

    /// <summary>Strips a trailing <c>; charset=...</c>-style parameter and normalizes case, so a
    /// caller-declared mime type like <c>"Application/PDF; charset=binary"</c> still matches.</summary>
    private static string Normalize(string mimeType) =>
        mimeType.Split(';')[0].Trim().ToLowerInvariant();

    private static NativeTextExtractionResult ExtractDocx(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var stream = new MemoryStream(content.ToArray());
            using var document = WordprocessingDocument.Open(stream, isEditable: false);

            var text = document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;

            return new NativeTextExtractionResult([new DocumentPageText(1, text)], IsSufficient: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Untrusted, caller-supplied bytes claiming to be a docx: any parse failure (corrupt
            // zip, not actually an OOXML package, ...) degrades to "insufficient" rather than
            // crashing the pipeline on one bad upload.
            return new NativeTextExtractionResult([], IsSufficient: false);
        }
    }

    private static NativeTextExtractionResult ExtractXlsx(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var stream = new MemoryStream(content.ToArray());
            using var document = SpreadsheetDocument.Open(stream, isEditable: false);

            var workbookPart = document.WorkbookPart;
            var sheets = workbookPart?.Workbook?.Sheets?.Elements<Sheet>() ?? [];
            var sharedStrings = workbookPart?.SharedStringTablePart?.SharedStringTable;

            var pages = new List<DocumentPageText>();

            foreach (var sheet in sheets)
            {
                if (workbookPart is null
                    || sheet.Id?.Value is not { } relationshipId
                    || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart
                    || worksheetPart.Worksheet is not { } worksheet)
                {
                    continue;
                }

                var builder = new StringBuilder();

                foreach (var row in worksheet.Descendants<Row>())
                {
                    foreach (var cell in row.Elements<Cell>())
                    {
                        var cellText = ReadCellText(cell, sharedStrings);
                        if (!string.IsNullOrEmpty(cellText))
                        {
                            builder.Append(cellText).Append(' ');
                        }
                    }
                }

                // One "page" per worksheet — XLSX has no native page concept without a rendering
                // engine (no fixed print layout is guaranteed), but "sheet" is the closest,
                // honestly-meaningful unit to cite as evidence (spec §7.3 source page/section).
                pages.Add(new DocumentPageText(pages.Count + 1, builder.ToString().TrimEnd()));
            }

            return new NativeTextExtractionResult(pages, IsSufficient: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new NativeTextExtractionResult([], IsSufficient: false);
        }
    }

    private static string? ReadCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        // Inline strings (<is><t>...</t></is>) carry their text directly on the cell, not via the
        // <v>/CellValue element the other two branches below read — a writer that has no
        // SharedStringTablePart at all (a real, common case, not just a test convenience) still
        // needs its cell text read correctly.
        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.Text?.Text;
        }

        var rawValue = cell.CellValue?.InnerText;
        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex))
        {
            return sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(sharedIndex)?.InnerText;
        }

        return rawValue;
    }

    /// <summary>Below this average non-whitespace character count per counted page, a PDF is
    /// treated as scanned/low-text rather than a real born-digital document (ADR-017: "native
    /// text... with <em>sufficient</em> extractable text"). This task's own tunable choice, not a
    /// locked number — mirrors <see cref="StagedExtractionService.LowConfidenceThreshold"/>'s
    /// same "documented constant, not a magic number buried in logic" pattern.</summary>
    private const int MinNonWhitespaceCharsPerPage = 40;

    /// <summary>How far back from a <c>stream</c> keyword this scan looks for that stream
    /// object's own dictionary (to check for <c>/FlateDecode</c>) — generous enough for any
    /// realistic content-stream dictionary (typically just <c>/Length</c> and <c>/Filter</c>)
    /// without scanning the entire preceding file.</summary>
    private const int DictionaryLookbackWindow = 1000;

    private static readonly Regex PageObjectRegex = new(
        @"(?<![A-Za-z])/Type\s*/Page(?![A-Za-z])", RegexOptions.Compiled);

    private static readonly Regex TextObjectRegex = new(
        @"BT(?<body>.*?)ET", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LiteralStringRegex = new(
        @"\((?<text>(?:[^()\\]|\\.)*)\)", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// A lightweight, self-contained PDF content-stream scan — deliberately not a full PDF
    /// parser. It does <b>not</b> parse the cross-reference table or object/page tree at all; it
    /// only:
    /// <list type="number">
    /// <item>Counts <c>/Type /Page</c> occurrences in the plain (uncompressed) file bytes as the
    /// page count.</item>
    /// <item>Finds every <c>stream ... endstream</c> block, decodes it if uncompressed or
    /// <c>/FlateDecode</c>-compressed (skips other filters — those are image/font encodings,
    /// never text), and keeps only the ones whose decoded body contains at least one
    /// <c>BT ... ET</c> text object (image/font/metadata streams never do) as "content stream"
    /// candidates.</item>
    /// <item>Requires the candidate content-stream count to equal the counted page count before
    /// trusting a 1-based, in-file-order pairing between them — on any mismatch this reports
    /// <see cref="NativeTextExtractionResult.IsSufficient"/> = <see langword="false"/> rather than
    /// guess, because a wrong page-number pairing would be exactly the "fabricated precision"
    /// product principle Appendix C rule 10 forbids on a real evidence citation.</item>
    /// <item>Within each paired stream, extracts literal-string operands of <c>Tj</c>/array
    /// members of <c>TJ</c> text-showing operators, with only the common escape sequences
    /// unescaped (<c>\( \) \\ \n \r \t</c>).</item>
    /// </list>
    /// Known, deliberate gaps (each one fails toward OCR, never toward a wrong answer): PDF 1.5+
    /// compressed object streams (a page tree living only inside one is invisible to the plain
    /// byte scan); hex-string (<c>&lt;...&gt;Tj</c>) text showing; custom/CID font encodings
    /// (extracted "text" may be glyph-index bytes rather than characters, which can occasionally
    /// still exceed <see cref="MinNonWhitespaceCharsPerPage"/> — the one gap that is not purely
    /// conservative; a real PDF text library is the fix if this proves insufficient in practice).
    /// </summary>
    private static NativeTextExtractionResult ExtractPdfNatively(ReadOnlyMemory<byte> content)
    {
        // Latin-1 maps every byte value 0-255 to exactly one char and back losslessly, so
        // slicing/searching this string and re-encoding a slice to bytes never corrupts binary
        // (possibly FlateDecode-compressed) stream content — it is purely a convenient view over
        // the same bytes, not a text decoding of the document's actual content.
        var raw = Encoding.Latin1.GetString(content.Span);

        var pageCount = PageObjectRegex.Count(raw);
        if (pageCount == 0)
        {
            return new NativeTextExtractionResult([], IsSufficient: false);
        }

        var streamTexts = FindTextContentStreams(raw);
        if (streamTexts.Count != pageCount)
        {
            return new NativeTextExtractionResult([], IsSufficient: false);
        }

        var pages = streamTexts
            .Select((text, index) => new DocumentPageText(index + 1, text))
            .ToList();

        var totalNonWhitespaceChars = pages.Sum(p => p.Text.Count(c => !char.IsWhiteSpace(c)));
        var isSufficient = totalNonWhitespaceChars / (double)pageCount >= MinNonWhitespaceCharsPerPage;

        return new NativeTextExtractionResult(isSufficient ? pages : [], isSufficient);
    }

    private static List<string> FindTextContentStreams(string raw)
    {
        var texts = new List<string>();
        var searchFrom = 0;

        while (true)
        {
            var streamKeywordIndex = raw.IndexOf("stream", searchFrom, StringComparison.Ordinal);
            if (streamKeywordIndex < 0)
            {
                break;
            }

            var bodyStart = streamKeywordIndex + "stream".Length;
            if (bodyStart < raw.Length && raw[bodyStart] == '\r')
            {
                bodyStart++;
            }

            if (bodyStart < raw.Length && raw[bodyStart] == '\n')
            {
                bodyStart++;
            }

            var endStreamIndex = raw.IndexOf("endstream", bodyStart, StringComparison.Ordinal);
            if (endStreamIndex < 0)
            {
                break;
            }

            var lookbackStart = Math.Max(0, streamKeywordIndex - DictionaryLookbackWindow);
            var dictionary = raw[lookbackStart..streamKeywordIndex];
            var body = raw[bodyStart..endStreamIndex];

            var decodedBody = TryDecodeStreamBody(dictionary, body);
            if (decodedBody is not null)
            {
                var text = ExtractTextShowingOperators(decodedBody);
                if (TextObjectRegex.IsMatch(decodedBody))
                {
                    // Only counted as a *content* stream (vs. an image/font/metadata stream that
                    // happened to decode cleanly) when it structurally contains at least one text
                    // object — see this class's own remarks on ExtractPdfNatively.
                    texts.Add(text);
                }
            }

            searchFrom = endStreamIndex + "endstream".Length;
        }

        return texts;
    }

    private static string? TryDecodeStreamBody(string dictionary, string body)
    {
        var isFlateEncoded = dictionary.Contains("/FlateDecode", StringComparison.Ordinal);
        var hasOtherFilter = !isFlateEncoded && dictionary.Contains("/Filter", StringComparison.Ordinal);

        if (hasOtherFilter)
        {
            // A recognized-but-unsupported filter (commonly an image codec: /DCTDecode (JPEG),
            // /CCITTFaxDecode (fax/scan), /JPXDecode (JPEG2000)) — never a text content stream,
            // so skipping it is correct, not a gap.
            return null;
        }

        if (!isFlateEncoded)
        {
            // No /Filter at all: the spec allows a verbatim (uncompressed) content stream.
            return body;
        }

        try
        {
            var compressedBytes = Encoding.Latin1.GetBytes(body);
            using var compressed = new MemoryStream(compressedBytes);
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            zlib.CopyTo(decompressed);

            return Encoding.Latin1.GetString(decompressed.ToArray());
        }
        catch (InvalidDataException)
        {
            // Declared /FlateDecode but the bytes are not valid zlib (corrupt file, or this
            // "stream...endstream" match was actually a cross-reference/object stream this scan
            // does not understand) — skip rather than throw.
            return null;
        }
    }

    private static string ExtractTextShowingOperators(string streamText)
    {
        var builder = new StringBuilder();

        foreach (Match textObject in TextObjectRegex.Matches(streamText))
        {
            foreach (Match literal in LiteralStringRegex.Matches(textObject.Groups["body"].Value))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(UnescapeLiteralString(literal.Groups["text"].Value));
            }
        }

        return builder.ToString();
    }

    private static string UnescapeLiteralString(string escaped)
    {
        var builder = new StringBuilder(escaped.Length);

        for (var i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] != '\\' || i == escaped.Length - 1)
            {
                builder.Append(escaped[i]);
                continue;
            }

            // Common escapes only — PDF octal escapes (\ddd) and spec-legal-but-rare balanced,
            // unescaped nested parentheses are not decoded. An under-decoded literal only makes
            // the sufficiency character count slightly conservative; it never invents content
            // that is not in the source bytes.
            var next = escaped[i + 1];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => next,
            });
            i++;
        }

        return builder.ToString();
    }
}
