using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

using minutesheet.Services.OpenRouter;

namespace minutesheet.Services;

public class DocumentSummarizationService
{
    private readonly IOpenRouterClient _openRouterClient;
    private readonly ILogger<DocumentSummarizationService> _logger;

    public DocumentSummarizationService(IOpenRouterClient openRouterClient, ILogger<DocumentSummarizationService> logger)
    {
        _openRouterClient = openRouterClient;
        _logger = logger;
    }

    public async Task<SummaryResult> GenerateSummaryAsync(
        string category, 
        string creatorName, 
        string creatorDesignation, 
        string creatorDepartment, 
        string creatorEmpNo, 
        string descriptionHtml, 
        IBrowserFile? file)
    {
        var text = await ExtractTextAsync(category, creatorName, creatorDesignation, creatorDepartment, creatorEmpNo, descriptionHtml, file);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SummaryResult("No text available to summarize.", new List<string>(), new List<string>());
        }
        return await SummarizeAsync(text);
    }

    private async Task<string> ExtractTextAsync(
        string category, 
        string creatorName, 
        string creatorDesignation, 
        string creatorDepartment, 
        string creatorEmpNo, 
        string html, 
        IBrowserFile? file)
    {
        // Strip HTML to get plain text
        string plainText = Regex.Replace(html ?? "", "<.*?>", String.Empty).Trim();

        string attachmentText = string.Empty;
        if (file != null)
        {
            try
            {
                var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                using var stream = file.OpenReadStream(10 * 1024 * 1024); // max 10MB
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                if (ext == ".pdf")
                {
                    using var pdfDocument = PdfDocument.Open(memoryStream);
                    foreach (var page in pdfDocument.GetPages())
                    {
                        attachmentText += page.Text + " ";
                    }
                }
                else if (ext == ".doc" || ext == ".docx")
                {
                    using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
                    var body = wordDoc.MainDocumentPart?.Document.Body;
                    if (body != null)
                    {
                        attachmentText = body.InnerText;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract text from attachment: {FileName}", file.Name);
                attachmentText = $"[Attachment text could not be extracted. Filename: {file.Name}]";
            }
        }

        // Clean text to remove null bytes or control characters that could cause the AI model to hallucinate or trigger safety filters
        if (!string.IsNullOrEmpty(attachmentText))
        {
            attachmentText = new string(attachmentText.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
        }

        return $"Category: {category}\nPrepared By: {creatorName}\nDesignation: {creatorDesignation}\nDepartment: {creatorDepartment}\nEmp#: {creatorEmpNo}\n\nDescription:\n{plainText}\n\nAttachment Text:\n{attachmentText}";
    }


    private async Task<SummaryResult> SummarizeAsync(string documentText)
    {
        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are a minute-sheet summarizer. Given the provided text, produce a concise professional summary and extract the key actions and decisions. Respond ONLY with a single JSON object shaped exactly like {\"summary\": \"...\", \"actions\": [\"...\", \"...\"], \"decisions\": [\"...\", \"...\"]}. An 'action' is something that must be done or followed up (who does what by when). A 'decision' is a resolution, conclusion, or agreement reached in the meeting. Extract EVERY decision mentioned in the text. If the text does not state an explicit decision, capture the main conclusions, resolutions, or agreed takeaways as decisions instead of leaving the array empty. The decisions array must be non-empty whenever the text contains any meaningful outcome or conclusion. Regardless of whether the input text is in English, Urdu script, or Roman Urdu, the summary, every action and every decision MUST be written in English." },
                new { role = "user", content = documentText }
            }
        };

        var responseText = await _openRouterClient.SendChatCompletionAsync(0, payload);
        if (responseText == null)
        {
            return new SummaryResult("Error: AI API Key is not configured. Please add it to your user secrets.", new List<string>(), new List<string>());
        }

        if (responseText.StartsWith("Error:"))
        {
            return new SummaryResult(responseText, new List<string>(), new List<string>());
        }

        return ParseSummaryResult(responseText);
    }

    /// <summary>
    /// Parses the AI's JSON reply of the shape
    /// {"summary": "...", "actions": [...], "decisions": [...]}.
    /// Falls back to the raw text as the summary when the reply isn't valid JSON.
    /// </summary>
    private static SummaryResult ParseSummaryResult(string? generatedText)
    {
        if (string.IsNullOrWhiteSpace(generatedText))
        {
            return new SummaryResult("Summary could not be generated.", new List<string>(), new List<string>());
        }

        try
        {
            var cleaned = CleanJsonString(generatedText);

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var summaryProp)
                ? summaryProp.ValueKind == JsonValueKind.String ? summaryProp.GetString() ?? "" : ""
                : "";

            var actions = new List<string>();
            if (root.TryGetProperty("actions", out var actionsProp) && actionsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in actionsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        actions.Add(item.GetString() ?? "");
                    }
                }
            }

            var decisions = new List<string>();
            if (root.TryGetProperty("decisions", out var decisionsProp) && decisionsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in decisionsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        decisions.Add(item.GetString() ?? "");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(summary) || actions.Count > 0 || decisions.Count > 0)
            {
                return new SummaryResult(summary, actions, decisions);
            }
        }
        catch (JsonException)
        {
            // Fall through to the raw-text fallback below.
        }

        return new SummaryResult(generatedText.Trim(), new List<string>(), new List<string>());
    }

    public async Task<string> ExtractActionItemsAsync(string documentText)
    {
        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant. Extract a list of action items from the provided meeting notes. Return a JSON array named 'action_items' where each item contains a 'task', 'owner', and 'deadline'." },
                new { role = "user", content = documentText }
            }
        };

        var responseText = await _openRouterClient.SendChatCompletionAsync(1, payload);
        if (responseText == null) return "{\"error\": \"AI API Key is not configured.\"}";
        
        if (responseText.StartsWith("Error:")) return $"{{\"error\": \"{responseText}\"}}";

        return CleanJsonString(responseText) ?? "{\"action_items\": []}";
    }

    public async Task<string> GenerateAgendaAsync(string documentText)
    {
        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant. Analyze the provided meeting notes and suggest a next-meeting agenda. Output a valid JSON array named 'agenda_items'. Each item must contain a 'topic' (what to discuss), an 'owner' (who leads the topic), and a 'time_box' (e.g., '10 mins')." },
                new { role = "user", content = documentText }
            }
        };

        var responseText = await _openRouterClient.SendChatCompletionAsync(2, payload);
        if (responseText == null) return "{\"error\": \"AI API Key is not configured.\"}";

        if (responseText.StartsWith("Error:")) return $"{{\"error\": \"{responseText}\"}}";

        return CleanJsonString(responseText) ?? "{\"agenda_items\": []}";
    }

    public async Task<string> TranslateUrduToEnglishAsync(string urduText)
    {
        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            messages = new[]
            {
                new { role = "system", content = "You are an expert translator. Translate the following Urdu text (which may be in Urdu script or Roman Urdu) into professional English. Respond ONLY with the English translation, and nothing else." },
                new { role = "user", content = urduText }
            }
        };

        var responseText = await _openRouterClient.SendChatCompletionAsync(3, payload);
        if (responseText == null) return "Error: AI API Key is not configured.";

        return responseText.Trim();
    }
    private static string? CleanJsonString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var cleaned = input.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstLineBreak = cleaned.IndexOf('\n');
            if (firstLineBreak > 0) cleaned = cleaned[(firstLineBreak + 1)..];
            var fence = cleaned.LastIndexOf("```");
            if (fence > 0) cleaned = cleaned[..fence];
            cleaned = cleaned.Trim();
        }
        return cleaned;
    }
}

public class SummaryResult
{
    public SummaryResult(string summary, List<string> actions, List<string> decisions)
    {
        Summary = summary;
        Actions = actions;
        Decisions = decisions;
    }

    public string Summary { get; set; }
    public List<string> Actions { get; set; }
    public List<string> Decisions { get; set; }
}

public class ActionItemDto
{
    [System.Text.Json.Serialization.JsonPropertyName("task")]
    public string Task { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("deadline")]
    public string Deadline { get; set; } = "";
}

public class ActionItemsResponseDto
{
    [System.Text.Json.Serialization.JsonPropertyName("action_items")]
    public List<ActionItemDto> ActionItems { get; set; } = new();
}

public class AgendaItemDto
{
    [System.Text.Json.Serialization.JsonPropertyName("topic")]
    public string Topic { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("owner")]
    public string Owner { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("time_box")]
    public string TimeBox { get; set; } = "";
}

public class AgendaResponseDto
{
    [System.Text.Json.Serialization.JsonPropertyName("agenda_items")]
    public List<AgendaItemDto> AgendaItems { get; set; } = new();
}
