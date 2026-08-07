using System.Net;
using System.Text.RegularExpressions;
using minutesheet.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace minutesheet.Services
{
    // Renders a minute sheet into a shareable PDF attachment.
    public sealed class SheetPdfService
    {
        private readonly IWebHostEnvironment _env;

        public SheetPdfService(IWebHostEnvironment env)
        {
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] Build(MinuteSheet sheet, string creatorName)
        {
            var status = sheet.Status ?? ApprovalWorkflow.StatusFor(sheet);
            var actionItems = ParseActionItems(sheet.ActionItems);
            var logoBytes = LogoBytes();

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(t => t.FontSize(11).FontColor("#1a1d2e"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            if (logoBytes.Length > 0)
                            {
                                row.AutoItem().Width(170).Image(logoBytes);
                            }
                            row.RelativeItem().AlignRight().Text("MINUTE SHEET")
                                .FontSize(20).Bold().FontColor("#000066");
                            if (sheet.IsConfidential)
                            {
                                row.AutoItem().Container().Background("#fdeceb").PaddingVertical(4).PaddingHorizontal(6)
                                    .Text("CONFIDENTIAL").FontSize(10).Bold().FontColor("#d33636");
                            }
                        });
                        col.Item().PaddingTop(2).Text(sheet.CreatedAt.ToLocalTime().ToString("dd MMMM yyyy, HH:mm"))
                            .FontSize(10).FontColor("#767b94");
                        col.Item().PaddingBottom(8).LineHorizontal(1).LineColor("#e7e8f2");
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().PaddingBottom(2).Text(text =>
                        {
                            text.Span("Category:  ").Bold().FontColor("#000066");
                            text.Span(sheet.Category.ToString());
                        });
                        col.Item().PaddingBottom(2).Text($"Prepared by:  {creatorName}");
                        col.Item().PaddingBottom(8).Text($"Status:  {status}");

                        if (actionItems.Count > 0)
                        {
                            col.Item().PaddingTop(4).Text("Action Items").FontSize(14).Bold().FontColor("#000066");
                            foreach (var item in actionItems)
                            {
                                var meta = new List<string>();
                                if (!string.IsNullOrWhiteSpace(item.Owner)) meta.Add($"Owner: {item.Owner}");
                                if (!string.IsNullOrWhiteSpace(item.Deadline)) meta.Add($"Deadline: {item.Deadline}");
                                col.Item().PaddingLeft(6).Text($"•  {item.Task}" + (meta.Count > 0 ? $"  ({string.Join(" · ", meta)})" : ""));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(sheet.Summary))
                        {
                            col.Item().PaddingTop(8).Text("Summary").FontSize(14).Bold().FontColor("#000066");
                            col.Item().PaddingBottom(4).Text(ToPlainText(sheet.Summary));
                        }

                        col.Item().PaddingTop(8).Text("Description").FontSize(14).Bold().FontColor("#000066");
                        col.Item().PaddingBottom(4).Text(ToPlainText(sheet.DescriptionHtml));

                        if (!string.IsNullOrWhiteSpace(sheet.AttachmentFileName))
                        {
                            col.Item().PaddingTop(8).Text($"Attachment:  {sheet.AttachmentFileName}");
                        }

                        if (sheet.ApprovalSteps.Count > 0)
                        {
                            col.Item().PaddingTop(8).Text("Approval workflow").FontSize(14).Bold().FontColor("#000066");
                            foreach (var step in sheet.ApprovalSteps.OrderBy(s => s.StepIndex))
                            {
                                col.Item().PaddingLeft(6).Text($"Step {step.StepIndex}:  {step.ApproverEmail}  —  {step.Action} ({StatusName(step.Status)})");
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.DefaultTextStyle(t => t.FontSize(9).FontColor("#999"));
                        x.CurrentPageNumber();
                    });
                });
            });

            return doc.GeneratePdf();
        }

        private byte[] LogoBytes()
        {
            var path = Path.Combine(_env.WebRootPath, "images", "FFC-Logo-Blue-V3.webp");
            return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
        }

        private static List<ActionItemDto> ParseActionItems(string? json)
        {
            var items = new List<ActionItemDto>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return items;
            }
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<ActionItemDto>>(json);
                if (list is not null)
                {
                    items.AddRange(list.Where(i => !string.IsNullOrWhiteSpace(i.Task)));
                }
            }
            catch
            {
                // Ignore malformed JSON.
            }
            return items;
        }

        private static string ToPlainText(string htmlOrText)
        {
            var withoutTags = Regex.Replace(htmlOrText ?? "", "<[^>]*>", " ");
            return WebUtility.HtmlDecode(withoutTags).Trim();
        }

        private static string StatusName(ApprovalStepStatus s) => s switch
        {
            ApprovalStepStatus.Approved => "Approved",
            ApprovalStepStatus.Reviewed => "Reviewed",
            _ => "Pending"
        };
    }
}