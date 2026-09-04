using System.IO.Compression;
using System.Text;
using Contigo.Documents.Contracts.Application.Extraction;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace Contigo.Documents.Contracts.Tests;

/// <summary>
/// Proves <see cref="NativeDocumentTextExtractor"/> — the concrete, real native-text half of task
/// E02/F01/US02/T02's hybrid pre-pass. DOCX/XLSX round-trip through the real
/// <c>DocumentFormat.OpenXml</c> SDK (no hand-rolled binary — the SDK's own writer builds the test
/// fixtures). PDF has no such library backing it (see the type's own doc comment for why); its own
/// remarks on <c>ExtractPdfNatively</c> spell out exactly what this suite proves and why hand-built
/// byte fixtures are safe here — this scanner never reads a cross-reference table or object
/// numbering, only the literal markers these fixtures place.
/// </summary>
public sealed class NativeDocumentTextExtractorTests
{
    private const string PdfMimeType = "application/pdf";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    public void CanHandle_recognizes_pdf_docx_and_xlsx_but_not_an_image_mime_type()
    {
        var extractor = new NativeDocumentTextExtractor();

        Assert.True(extractor.CanHandle(PdfMimeType));
        Assert.True(extractor.CanHandle(DocxMimeType));
        Assert.True(extractor.CanHandle(XlsxMimeType));
        Assert.False(extractor.CanHandle("image/png"));
    }

    [Fact]
    public void CanHandle_tolerates_a_charset_suffix_and_case_differences()
    {
        var extractor = new NativeDocumentTextExtractor();

        Assert.True(extractor.CanHandle("Application/PDF; charset=binary"));
    }

    // ---- DOCX ------------------------------------------------------------------------------

    [Fact]
    public void Docx_text_is_extracted_as_a_single_page_and_always_sufficient()
    {
        var bytes = BuildMinimalDocx("This is a real Word document with actual contract text.");
        var extractor = new NativeDocumentTextExtractor();

        var result = extractor.Extract(DocxMimeType, bytes);

        Assert.True(result.IsSufficient);
        var page = Assert.Single(result.Pages);
        Assert.Equal(1, page.PageNumber);
        Assert.Contains("real Word document", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupted_docx_bytes_are_insufficient_not_a_crash()
    {
        var extractor = new NativeDocumentTextExtractor();

        var result = extractor.Extract(DocxMimeType, "not actually a zip archive"u8.ToArray());

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    private static byte[] BuildMinimalDocx(string text)
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new WP.Document(new WP.Body(new WP.Paragraph(new WP.Run(new WP.Text(text)))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ---- XLSX ------------------------------------------------------------------------------

    [Fact]
    public void Xlsx_reads_shared_string_and_inline_string_cells_from_every_worksheet()
    {
        var bytes = BuildMinimalXlsx();
        var extractor = new NativeDocumentTextExtractor();

        var result = extractor.Extract(XlsxMimeType, bytes);

        Assert.True(result.IsSufficient);
        var page = Assert.Single(result.Pages);
        Assert.Equal(1, page.PageNumber);
        Assert.Contains("Alpha", page.Text, StringComparison.Ordinal);
        Assert.Contains("Beta", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupted_xlsx_bytes_are_insufficient_not_a_crash()
    {
        var extractor = new NativeDocumentTextExtractor();

        var result = extractor.Extract(XlsxMimeType, "not actually a zip archive either"u8.ToArray());

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    private static byte[] BuildMinimalXlsx()
    {
        using var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringPart.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("Alpha")));
            sharedStringPart.SharedStringTable.Save();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(
                new SheetData(
                    new Row(
                        new Cell { CellReference = "A1", DataType = CellValues.SharedString, CellValue = new CellValue("0") },
                        new Cell { CellReference = "B1", DataType = CellValues.InlineString, InlineString = new InlineString(new Text("Beta")) })));
            worksheetPart.Worksheet.Save();

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Sheet1" });
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    // ---- PDF -------------------------------------------------------------------------------

    [Fact]
    public void Pdf_with_matching_page_and_content_stream_counts_and_real_text_is_sufficient()
    {
        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            "2 0 obj << /Length 100 >>\n" +
            "stream\n" +
            "BT (This is the first page of a real contract with plenty of readable text.) Tj ET\n" +
            "endstream\n" +
            "endobj\n" +
            "3 0 obj << /Type /Page >> endobj\n" +
            "4 0 obj << /Length 100 >>\n" +
            "stream\n" +
            "BT (This is the second page, also containing plenty of readable contract text.) Tj ET\n" +
            "endstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.True(result.IsSufficient);
        Assert.Equal(2, result.Pages.Count);
        Assert.Equal(1, result.Pages[0].PageNumber);
        Assert.Contains("first page", result.Pages[0].Text, StringComparison.Ordinal);
        Assert.Equal(2, result.Pages[1].PageNumber);
        Assert.Contains("second page", result.Pages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_page_with_no_text_operators_at_all_is_insufficient()
    {
        // No BT/ET anywhere in the content stream — an image-only ("scanned") page paints its
        // content via an XObject `Do` operator, never a text-showing operator.
        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            "2 0 obj << /Length 20 >>\n" +
            "stream\n" +
            "/Im0 Do\n" +
            "endstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Pdf_with_more_pages_than_text_content_streams_is_insufficient_rather_than_guessed()
    {
        // Two /Type/Page objects but only one text-bearing content stream: pairing them 1:1 would
        // risk a wrong page-number citation, so this must defer to OCR instead of guessing.
        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            "2 0 obj << /Type /Page >> endobj\n" +
            "3 0 obj << /Length 60 >>\n" +
            "stream\n" +
            "BT (Only one content stream for two declared pages.) Tj ET\n" +
            "endstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Pdf_below_the_minimum_characters_per_page_is_insufficient()
    {
        // Real /Type/Page + a real text-bearing stream, but far too little text per page to be a
        // genuine born-digital contract page (e.g. a lone page number "3" on an otherwise scanned page).
        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            "2 0 obj << /Length 20 >>\n" +
            "stream\n" +
            "BT (3) Tj ET\n" +
            "endstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.False(result.IsSufficient);
    }

    [Fact]
    public void Garbage_bytes_claiming_to_be_pdf_are_insufficient_not_a_crash()
    {
        var extractor = new NativeDocumentTextExtractor();
        byte[] garbage = [0x00, 0x01, 0x02, 0xFF, 0xFE, 0x10, 0x20, 0x30, 0x40, 0x50];

        var result = extractor.Extract(PdfMimeType, garbage);

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Pdf_with_a_flate_compressed_content_stream_is_inflated_and_read()
    {
        const string pageText = "Compressed page text should still be extracted correctly by this scanner.";
        var compressed = ZlibCompress(Encoding.Latin1.GetBytes($"BT ({pageText}) Tj ET"));
        var compressedAsLatin1 = Encoding.Latin1.GetString(compressed);

        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            $"2 0 obj << /Filter /FlateDecode /Length {compressed.Length} >>\n" +
            "stream\n" +
            compressedAsLatin1 +
            "\nendstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.True(result.IsSufficient);
        var page = Assert.Single(result.Pages);
        Assert.Contains("Compressed page text", page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_content_stream_using_an_unsupported_image_filter_is_skipped_not_scanned_as_garbage()
    {
        // /DCTDecode (JPEG) is an image codec, never a text content stream — this must not be
        // scanned for BT/ET (it would find none anyway, but the point is it is skipped cleanly,
        // not fed byte-for-byte into the text scanner as if it might be raw content).
        var pdf =
            "%PDF-1.4\n" +
            "1 0 obj << /Type /Page >> endobj\n" +
            "2 0 obj << /Filter /DCTDecode /Length 10 >>\n" +
            "stream\n" +
            "ÿØÿàbinary\n" +
            "endstream\n" +
            "endobj\n" +
            "%%EOF\n";

        var extractor = new NativeDocumentTextExtractor();
        var result = extractor.Extract(PdfMimeType, Encoding.Latin1.GetBytes(pdf));

        Assert.False(result.IsSufficient);
        Assert.Empty(result.Pages);
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();

        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
}
