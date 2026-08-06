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
            var (actions, decisions) = ParseActionsDecisions(sheet.ActionsDecisions);
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

                        if (actions.Count > 0 || decisions.Count > 0)
                        {
                            col.Item().PaddingTop(4).Text("Actions & Decisions").FontSize(14).Bold().FontColor("#000066");
                            if (actions.Count > 0)
                            {
                                col.Item().PaddingTop(4).Text("ACTIONS").FontSize(11).Bold().FontColor("#d33636");
                                foreach (var item in actions)
                                {
                                    col.Item().PaddingLeft(6).Text($"•  {item}");
                                }
                            }
                            if (decisions.Count > 0)
                            {
                                col.Item().PaddingTop(6).Text("DECISIONS").FontSize(11).Bold().FontColor("#1a8a5a");
                                foreach (var item in decisions)
                                {
                                    col.Item().PaddingLeft(6).Text($"•  {item}");
                                }
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

        private static (List<string> Actions, List<string> Decisions) ParseActionsDecisions(string? json)
        {
            var actions = new List<string>();
            var decisions = new List<string>();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("actions", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in a.EnumerateArray())
                        {
                            var text = item.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(text)) actions.Add(text);
                        }
                    }
                    if (root.TryGetProperty("decisions", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in d.EnumerateArray())
                        {
                            var text = item.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(text)) decisions.Add(text);
                        }
                    }
                }
                catch
                {
                    // Ignore malformed JSON.
                }
            }
            return (actions, decisions);
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