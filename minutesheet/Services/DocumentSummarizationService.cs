using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace minutesheet.Services;

public class DocumentSummarizationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentSummarizationService> _logger;

    public DocumentSummarizationService(HttpClient httpClient, IConfiguration configuration, ILogger<DocumentSummarizationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
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
        var apiKey = _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("AI API Key is not configured in OpenRouterSettings:ApiKey.");
            return new SummaryResult("Error: AI API Key is not configured. Please add it to your user secrets.", new List<string>(), new List<string>());
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";

        var payload = new
        {
            model = "openai/gpt-oss-20b:free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are a minute-sheet summarizer. Given the provided text, produce a concise professional summary and extract the key actions and decisions. Respond ONLY with a single JSON object shaped exactly like {\"summary\": \"...\", \"actions\": [\"...\", \"...\"], \"decisions\": [\"...\", \"...\"]}. An 'action' is something that must be done or followed up (who does what by when). A 'decision' is a resolution, conclusion, or agreement reached in the meeting. Extract EVERY decision mentioned in the text. If the text does not state an explicit decision, capture the main conclusions, resolutions, or agreed takeaways as decisions instead of leaving the array empty. The decisions array must be non-empty whenever the text contains any meaningful outcome or conclusion. Regardless of whether the input text is in English, Urdu script, or Roman Urdu, the summary, every action and every decision MUST be written in English." },
                new { role = "user", content = documentText }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "http://localhost:5285");
            request.Headers.Add("X-Title", "Minute Sheet App");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OpenRouter raw response: {ResponseJson}", responseJson);
            
            using var doc = JsonDocument.Parse(responseJson);
            
            var choice = doc.RootElement.GetProperty("choices")[0];
            
            var generatedText = choice
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (choice.TryGetProperty("finish_reason", out var finishReasonProp))
            {
                var finishReason = finishReasonProp.GetString();
                if (finishReason == "content_filter")
                {
                    return new SummaryResult("Error: The document text was flagged by the AI safety filter. Please review the content.", new List<string>(), new List<string>());
                }
            }

            // Fallback check in case the model returns safety strings directly in the content
            if (!string.IsNullOrWhiteSpace(generatedText) && generatedText.Trim().StartsWith("User Safety:"))
            {
                return new SummaryResult("Error: The document text was flagged by the AI safety filter. Please review the content.", new List<string>(), new List<string>());
            }

            return ParseSummaryResult(generatedText);
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)        {
            _logger.LogWarning(httpEx, "AI API rate limit reached (429)");
            return new SummaryResult("Error: AI service rate limit reached (429). Please try again in a moment or check your API quota.", new List<string>(), new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API");
            return new SummaryResult("Error: Failed to generate summary from AI service.", new List<string>(), new List<string>());
        }
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
            var cleaned = generatedText.Trim();
            if (cleaned.StartsWith("```"))
            {
                var firstLineBreak = cleaned.IndexOf('\n');
                if (firstLineBreak > 0) cleaned = cleaned[(firstLineBreak + 1)..];
                var fence = cleaned.LastIndexOf("```");
                if (fence > 0) cleaned = cleaned[..fence];
                cleaned = cleaned.Trim();
            }

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
        var apiKey = _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "{\"error\": \"AI API Key is not configured.\"}";
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";

        var payload = new
        {
            model = "openrouter/free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant. Extract a list of action items from the provided meeting notes. Return a JSON array named 'action_items' where each item contains a 'task', 'owner', and 'deadline'." },
                new { role = "user", content = documentText }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "http://localhost:5285");
            request.Headers.Add("X-Title", "Minute Sheet App");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OpenRouter raw response (action items): {ResponseJson}", responseJson);
            
            using var doc = JsonDocument.Parse(responseJson);
            var generatedText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return generatedText?.Trim() ?? "{\"action_items\": []}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API for action item extraction");
            return "{\"error\": \"Failed to extract action items.\"}";
        }
    }

    public async Task<string> GenerateAgendaAsync(string documentText)
    {
        var apiKey = _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "{\"error\": \"AI API Key is not configured.\"}";
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";

        var payload = new
        {
            model = "openrouter/free",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant. Analyze the provided meeting notes and suggest a next-meeting agenda. Output a valid JSON array named 'agenda_items'. Each item must contain a 'topic' (what to discuss), an 'owner' (who leads the topic), and a 'time_box' (e.g., '10 mins')." },
                new { role = "user", content = documentText }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "http://localhost:5285");
            request.Headers.Add("X-Title", "Minute Sheet App");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OpenRouter raw response (agenda): {ResponseJson}", responseJson);
            
            using var doc = JsonDocument.Parse(responseJson);
            var generatedText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return generatedText?.Trim() ?? "{\"agenda_items\": []}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API for agenda generation");
            return "{\"error\": \"Failed to generate agenda.\"}";
        }
    }

    public async Task<string> TranslateUrduToEnglishAsync(string urduText)
    {
        var apiKey = _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "Error: AI API Key is not configured.";
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";

        var payload = new
        {
            model = "openai/gpt-oss-20b:free",
            messages = new[]
            {
                new { role = "system", content = "You are an expert translator. Translate the following Urdu text (which may be in Urdu script or Roman Urdu) into professional English. Respond ONLY with the English translation, and nothing else." },
                new { role = "user", content = urduText }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "http://localhost:5285");
            request.Headers.Add("X-Title", "Minute Sheet App");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OpenRouter raw response (translation): {ResponseJson}", responseJson);
            
            using var doc = JsonDocument.Parse(responseJson);
            var generatedText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return generatedText?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API for translation");
            return $"[Translation Error: {ex.Message}]";
        }
    }
    // Parses the model's JSON {"summary": "...", "actions": [...], "decisions": [...]} response,
    // falling back to treating the whole response as plain summary text.
    private static SummaryResult ParseSummaryResult(string? generatedText)
    {
        if (!string.IsNullOrWhiteSpace(generatedText))
        {
            var trimmed = generatedText.Trim();

            // The model sometimes wraps JSON in a code fence.
            if (trimmed.StartsWith("```"))
            {
                var start = trimmed.IndexOf('{');
                var end = trimmed.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    trimmed = trimmed[start..(end + 1)];
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                var summary = root.TryGetProperty("summary", out var summaryProp)
                    ? summaryProp.GetString()?.Trim()
                    : null;

                var actions = ReadStringArray(root, "actions");
                var decisions = ReadStringArray(root, "decisions");

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return new SummaryResult(summary, actions, decisions);
                }
            }
            catch (JsonException)
            {
                // Not valid JSON — treat the raw text as the summary.
            }
        }

        return new SummaryResult(generatedText?.Trim() ?? "Summary could not be generated.", new List<string>(), new List<string>());
    }

    private static List<string> ReadStringArray(JsonElement root, string name)
    {
        var result = new List<string>();
        if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                var text = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(text);
                }
            }
        }
        return result;
    }
}

public sealed class SummaryResult
{
    public SummaryResult(string summary, List<string> actions, List<string> decisions)
    {
        Summary = summary;
        Actions = actions;
        Decisions = decisions;
    }

    public string Summary { get; }
    public List<string> Actions { get; }
    public List<string> Decisions { get; }
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

public sealed record SummaryResult(string Summary, List<string> Actions, List<string> Decisions);
