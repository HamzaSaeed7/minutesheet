using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using SkiaSharp;

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
        string Summary,
        byte[]? LogoPng = null);

    private const double Margin = 48;          // PDF points (~16.9mm)
    private const double PdfPageWidth = 595;   // A4 in points

    private static byte[]? _cachedLogoPng;
    private static readonly object LogoLock = new();

    /// <summary>
    /// Loads the FFC logo (WebP) from wwwroot and converts it to PNG so
    /// PdfSharp/OpenXml can embed it. Cached across calls.
    /// </summary>
    public static byte[]? LogoPng(string? webRootPath)
    {
        if (_cachedLogoPng is not null)
        {
            return _cachedLogoPng;
        }

        lock (LogoLock)
        {
            if (_cachedLogoPng is not null)
            {
                return _cachedLogoPng;
            }

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                return null;
            }

            var file = Path.Combine(webRootPath, "images", "FFC-Logo-Blue-V3.webp");
            if (!File.Exists(file))
            {
                return null;
            }

            try
            {
                using var data = SKData.Create(file);
                using var bitmap = SKBitmap.Decode(data);
                if (bitmap is null)
                {
                    return null;
                }

                using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                _cachedLogoPng = png?.ToArray();
            }
            catch
            {
                _cachedLogoPng = null;
            }

            return _cachedLogoPng;
        }
    }

    public static byte[] BuildDocx(ExportContent c)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            if (c.LogoPng is { Length: > 0 })
            {
                var imagePart = main.AddImagePart(ImagePartType.Png);
                using (var imgStream = new MemoryStream(c.LogoPng))
                {
                    imagePart.FeedData(imgStream);
                }

                var widthPx = 384;
                var heightPx = 128;
                try
                {
                    var dims = ImageDimensions(c.LogoPng);
                    widthPx = dims.Width;
                    heightPx = dims.Height;
                }
                catch
                {
                    // fall back to defaults
                }

                var logo = new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = "160" },
                    new Justification { Val = JustificationValues.Center }));
                var drawing = BuildLogoDrawing(main.GetIdOfPart(imagePart), widthPx, heightPx);
                logo.AppendChild(new Run(drawing));
                body.AppendChild(logo);
            }

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

            body.AppendChild(new SectionProperties(
                new PageMargin { Top = 1100, Bottom = 1100, Left = 1400, Right = 1400 }));
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

        if (c.LogoPng is { Length: > 0 })
        {
            try
            {
                using var imgStream = new MemoryStream(c.LogoPng);
                var logo = XImage.FromStream(imgStream);
                double logoWidth = 140;
                double logoHeight = logo.PixelHeight * (logoWidth / logo.PixelWidth);
                double x = (PdfPageWidth - logoWidth) / 2;
                gfx.DrawImage(logo, x, y, logoWidth, logoHeight);
                y += logoHeight + 10;
            }
            catch
            {
                // Logo is decorative; continue without it.
            }
        }

        y = DrawWrapped(ref gfx, c.Title, titleFont, y, right, center: true);
        y = DrawWrapped(ref gfx, "FFC Fertilizers", metaFont, y, right, center: true);
        y += 10;

        y = DrawWrapped(ref gfx, "Category: " + c.Category, metaFont, y, right);
        y = DrawWrapped(ref gfx, "Confidentiality: " + c.Confidentiality, metaFont, y, right);
        y = DrawWrapped(ref gfx, "Date: " + c.Date, metaFont, y, right);
        y = DrawWrapped(ref gfx, "Prepared by: " + c.PreparedBy, metaFont, y, right);
        y += 10;

        y = DrawWrapped(ref gfx, "Description", headingFont, y, right);
        y = DrawWrapped(ref gfx, c.Description, bodyFont, y, right);
        y += 8;

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            y = DrawWrapped(ref gfx, "Summary (AI Generated)", headingFont, y, right);
            y = DrawWrapped(ref gfx, c.Summary, bodyFont, y, right);
        }

        gfx.Dispose();
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static double DrawWrapped(
        ref XGraphics gfx, string text, XFont font, double y, double right,
        bool center = false, double maxY = PdfPageWidth * 1.4142 - 48)
    {
        if (string.IsNullOrEmpty(text))
        {
            return y;
        }

        const double lineHeightFactor = 1.35;
        var lineHeight = font.GetHeight() * lineHeightFactor;
        var leading = font.GetHeight();

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var paragraph in normalized.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                words = new[] { "" };
            }

            var line = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = line + " " + words[i];
                if (gfx.MeasureString(candidate, font).Width <= right - Margin)
                {
                    line = candidate;
                }
                else
                {
                    gfx = DrawLine(gfx, line, font, y, right, center, lineHeight, leading, maxY, out y);
                    line = words[i];
                }
            }
            gfx = DrawLine(gfx, line, font, y, right, center, lineHeight, leading, maxY, out y);
        }
        return y + 4;
    }

    private static XGraphics DrawLine(
        XGraphics gfx, string line, XFont font, double y, double right,
        bool center, double lineHeight, double leading, double maxY, out double newY)
    {
        if (y + lineHeight > maxY)
        {
            var owner = gfx.PdfPage.Owner;
            gfx.Dispose();
            var page = owner.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            y = Margin;
        }

        var size = gfx.MeasureString(line, font);
        var x = center ? (PdfPageWidth - size.Width) / 2 : Margin;
        gfx.DrawString(line, font, XBrushes.Black, new XPoint(x, y + leading));
        newY = y + lineHeight;
        return gfx;
    }

    private static (int Width, int Height) ImageDimensions(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        return bitmap is null ? (384, 128) : (bitmap.Width, bitmap.Height);
    }

    private static Drawing BuildLogoDrawing(string relationshipId, int widthPx, int heightPx)
    {
        const double emuPerPx = 9525;                 // 1px = 9525 EMU at 96 DPI
        var extCx = (long)(widthPx * emuPerPx);
        var extCy = (long)(heightPx * emuPerPx);

        var extent = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = extCx, Cy = extCy };
        var docProperties = new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties
        {
            Id = 1U,
            Name = "FFCLogo"
        };

        var blipFill = new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
            new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId },
            new DocumentFormat.OpenXml.Drawing.Stretch(
                new DocumentFormat.OpenXml.Drawing.FillRectangle()));

        var shapeProperties = new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
            new DocumentFormat.OpenXml.Drawing.Transform2D(
                new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                new DocumentFormat.OpenXml.Drawing.Extents { Cx = extCx, Cy = extCy }),
            new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                new DocumentFormat.OpenXml.Drawing.AdjustValueList())
            { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle });

        var picture = new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties
                {
                    Id = 1U,
                    Name = "FFCLogo"
                },
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
            blipFill,
            shapeProperties);

        var graphic = new DocumentFormat.OpenXml.Drawing.Graphic(
            new DocumentFormat.OpenXml.Drawing.GraphicData(picture)
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

        var inline = new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
            extent,
            docProperties,
            graphic)
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };

        return new Drawing(inline);
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
        run.PrependChild(NewRunProperties(size, bold));
        para.AppendChild(run);
        body.AppendChild(para);
    }

    private static void AddMeta(Body body, string label, string value)
    {
        var para = new Paragraph();
        var boldRun = new Run();
        boldRun.AppendChild(NewRunProperties(11, bold: true));
        boldRun.AppendChild(new Text($"{label}: "));
        var valueRun = new Run();
        valueRun.AppendChild(NewRunProperties(10, bold: false));
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
        run.AppendChild(NewRunProperties(13, bold: true));
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
            run.AppendChild(NewRunProperties(10, bold: false));
            run.AppendChild(new Text(paragraph) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
            body.AppendChild(para);
        }
    }

    /// <summary>
    /// Builds a RunProperties with children in schema order: rFonts, b, sz.
    /// </summary>
    private static RunProperties NewRunProperties(int fontSizePt, bool bold)
    {
        var props = new RunProperties();
        props.AppendChild(new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
        if (bold)
        {
            props.AppendChild(new Bold());
        }
        props.AppendChild(new FontSize { Val = (fontSizePt * 2).ToString() });
        return props;
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
