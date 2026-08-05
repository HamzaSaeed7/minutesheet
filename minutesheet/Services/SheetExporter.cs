using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace minutesheet.Services;

/// <summary>
/// Builds export documents (Word .doc/.docx and PDF) for a minute sheet's
/// AI summary and description, with a light FFC header block.
/// </summary>
public static class SheetExporter
{
    static SheetExporter()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new WindowsFontResolver();
        }
    }

    public sealed record ExportContent(
        string Title,
        string Category,
        string Confidentiality,
        string Date,
        string PreparedBy,
        string Description,
        string Summary);

    private const double Margin = 48;          // PDF points (~16.9mm)
    private const double PdfPageWidth = 595;   // A4 in points

    public static byte[] BuildDocx(ExportContent c)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(new SectionProperties(
                new PageMargin { Top = 1100, Bottom = 1100, Left = 1400, Right = 1400 }));

            AddParagraph(body, "FFC Fertilizers", bold: true, size: 28, center: true);
            AddParagraph(body, c.Title, bold: true, size: 20, center: true);
            AddParagraph(body, "", bold: false, size: 10);

            AddMeta(body, "Category", c.Category);
            AddMeta(body, "Confidentiality", c.Confidentiality);
            AddMeta(body, "Date", c.Date);
            AddMeta(body, "Prepared by", c.PreparedBy);

            AddParagraph(body, "", bold: false, size: 8);
            AddHeading(body, "Description");
            AddBodyParagraph(body, c.Description);

            if (!string.IsNullOrWhiteSpace(c.Summary))
            {
                AddHeading(body, "Summary (AI Generated)");
                AddBodyParagraph(body, c.Summary);
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// A legacy-compatible Word document. Produced as RTF with a .doc
    /// extension and application/msword content type, which Microsoft Word
    /// opens as a native .doc file.
    /// </summary>
    public static byte[] BuildDoc(ExportContent c)
    {
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}");
        sb.Append(@"\f0\fs24 ");
        sb.Append(@"\pard\qc\b\fs36 ").Append(Escape(c.Title)).Append(@"\b0\par\par");
        sb.Append(@"\pard\qr\fs20 FFC Fertilizers\par\par");
        sb.Append(@"\pard\ql\fs24 ");
        sb.Append(@"\b Category:\b0 ").Append(Escape(c.Category)).Append(@"\par ");
        sb.Append(@"\b Confidentiality:\b0 ").Append(Escape(c.Confidentiality)).Append(@"\par ");
        sb.Append(@"\b Date:\b0 ").Append(Escape(c.Date)).Append(@"\par ");
        sb.Append(@"\b Prepared by:\b0 ").Append(Escape(c.PreparedBy)).Append(@"\par\par ");
        sb.Append(@"\b\fs28 Description\b0\par ");
        sb.Append(Escape(c.Description)).Append(@"\par\par ");
        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            sb.Append(@"\b\fs28 Summary (AI Generated)\b0\par ");
            sb.Append(Escape(c.Summary)).Append(@"\par ");
        }
        sb.Append('}');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public static byte[] BuildPdf(ExportContent c)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 13, XFontStyleEx.Bold);
        var metaFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var bodyFont = new XFont("Arial", 10.5, XFontStyleEx.Regular);

        double y = Margin;
        double right = PdfPageWidth - Margin;

        y = DrawWrapped(gfx, c.Title, titleFont, y, right, center: true);
        y = DrawWrapped(gfx, "FFC Fertilizers", metaFont, y, right, center: true);
        y += 10;

        y = DrawWrapped(gfx, "Category: " + c.Category, metaFont, y, right);
        y = DrawWrapped(gfx, "Confidentiality: " + c.Confidentiality, metaFont, y, right);
        y = DrawWrapped(gfx, "Date: " + c.Date, metaFont, y, right);
        y = DrawWrapped(gfx, "Prepared by: " + c.PreparedBy, metaFont, y, right);
        y += 10;

        y = DrawWrapped(gfx, "Description", headingFont, y, right);
        y = DrawWrapped(gfx, c.Description, bodyFont, y, right);
        y += 8;

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            y = DrawWrapped(gfx, "Summary (AI Generated)", headingFont, y, right);
            y = DrawWrapped(gfx, c.Summary, bodyFont, y, right);
        }

        gfx.Dispose();
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static double DrawWrapped(
        XGraphics gfx, string text, XFont font, double y, double right,
        bool center = false, double maxY = PdfPageWidth * 1.4142 - 48)
    {
        if (string.IsNullOrEmpty(text))
        {
            return y;
        }

        const double lineHeightFactor = 1.35;
        var lineHeight = font.GetHeight() * lineHeightFactor;
        var leading = font.GetHeight();

        foreach (var line in Wrap(text, font, gfx, right - Margin))
        {
            if (y + lineHeight > maxY)
            {
                gfx.Dispose();
                var page = gfx.PdfPage.Owner.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = Margin;
            }

            var size = gfx.MeasureString(line, font);
            var x = center ? (PdfPageWidth - size.Width) / 2 : Margin;
            gfx.DrawString(line, font, XBrushes.Black, new XPoint(x, y + leading));
            y += lineHeight;
        }
        return y + 4;
    }

    private static IEnumerable<string> Wrap(string text, XFont font, XGraphics gfx, double maxWidth)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var paragraph in normalized.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield return "";
                continue;
            }

            var line = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = line + " " + words[i];
                if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    line = candidate;
                }
                else
                {
                    yield return line;
                    line = words[i];
                }
            }
            yield return line;
        }
    }

    private static void AddParagraph(Body body, string text, bool bold, int size, bool center = false)
    {
        var para = new Paragraph();
        if (center)
        {
            para.AppendChild(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
        }
        var run = new Run();
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var props = new RunProperties(new FontSize { Val = (size * 2).ToString() });
        if (bold)
        {
            props.AppendChild(new Bold());
        }
        props.AppendChild(new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
        run.PrependChild(props);
        para.AppendChild(run);
        body.AppendChild(para);
    }

    private static void AddMeta(Body body, string label, string value)
    {
        var para = new Paragraph();
        var boldRun = new Run();
        boldRun.AppendChild(new RunProperties(new Bold()) { RunFonts = new RunFonts { Ascii = "Arial", HighAnsi = "Arial" } });
        boldRun.AppendChild(new Text($"{label}: "));
        var valueRun = new Run();
        valueRun.AppendChild(new RunProperties(new FontSize { Val = "20" }) { RunFonts = new RunFonts { Ascii = "Arial", HighAnsi = "Arial" } });
        valueRun.AppendChild(new Text(value ?? "") { Space = SpaceProcessingModeValues.Preserve });
        para.AppendChild(boldRun);
        para.AppendChild(valueRun);
        body.AppendChild(para);
    }

    private static void AddHeading(Body body, string text)
    {
        var para = new Paragraph();
        para.AppendChild(new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }));
        var run = new Run();
        run.AppendChild(new RunProperties(new Bold()) { FontSize = new FontSize { Val = "26" }, RunFonts = new RunFonts { Ascii = "Arial", HighAnsi = "Arial" } });
        run.AppendChild(new Text(text));
        para.AppendChild(run);
        body.AppendChild(para);
    }

    private static void AddBodyParagraph(Body body, string text)
    {
        foreach (var paragraph in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "120" }));
            var run = new Run();
            run.AppendChild(new RunProperties(new FontSize { Val = "20" }) { RunFonts = new RunFonts { Ascii = "Arial", HighAnsi = "Arial" } });
            run.AppendChild(new Text(paragraph) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
            body.AppendChild(para);
        }
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s ?? "")
        {
            switch (ch)
            {
                case '\\': sb.Append(@"\\"); break;
                case '{': sb.Append(@"\{"); break;
                case '}': sb.Append(@"\}"); break;
                    default:
                        if (ch <= 127)
                        {
                            sb.Append(ch);
                        }
                        else
                        {
                            sb.Append(@"\u").Append((short)ch).Append('?');
                        }
                        break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// Resolves common Windows font faces from the system fonts directory so
/// PdfSharp can embed real glyphs. Falls back to the regular face with
/// simulated bold/italic when no dedicated style file exists.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    private static readonly string FontsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    private static readonly Dictionary<string, string> Faces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = "arial.ttf",
        ["Arial:B"] = "arialbd.ttf",
        ["Arial:I"] = "ariali.ttf",
        ["Arial:BI"] = "arialbi.ttf",
        ["Verdana"] = "verdana.ttf",
        ["Verdana:B"] = "verdanab.ttf",
        ["Verdana:I"] = "verdanai.ttf",
        ["Verdana:BI"] = "verdanaz.ttf",
        ["Times New Roman"] = "times.ttf",
        ["Times New Roman:B"] = "timesbd.ttf",
        ["Times New Roman:I"] = "timesi.ttf",
        ["Times New Roman:BI"] = "timesbi.ttf",
    };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var style = (isBold ? "B" : "") + (isItalic ? "I" : "");
        var key = style.Length == 0 ? familyName : $"{familyName}:{style}";

        if (Faces.TryGetValue(key, out var styled))
        {
            return new FontResolverInfo(styled);
        }

        if (Faces.TryGetValue(familyName, out var regular))
        {
            return new FontResolverInfo(regular, isBold, isItalic);
        }

        throw new InvalidOperationException($"No font file registered for family '{familyName}'.");
    }

    public byte[]? GetFont(string faceName)
    {
        var full = Path.Combine(FontsDir, faceName);
        return File.Exists(full) ? File.ReadAllBytes(full) : null;
    }
}
