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

    public async Task<string> GenerateSummaryAsync(
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
            return "No text available to summarize.";
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

    private async Task<string> SummarizeAsync(string documentText)
    {
        var apiKey = _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return "Error: AI API Key is not configured.";
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";

        var payload = new
        {
            model = "openrouter/free",
            messages = new[]
            {
                new { role = "system", content = "Provide a concise, professional summary of the entire provided text. Regardless of whether the input text is in English, Urdu script, or Roman Urdu, the final summary MUST be written in English." },
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
                    return "Error: The document text was flagged by the AI safety filter. Please review the content.";
                }
            }

            // Fallback check in case the model returns safety strings directly in the content
            if (!string.IsNullOrWhiteSpace(generatedText) && generatedText.Trim().StartsWith("User Safety:"))
            {
                return "Error: The document text was flagged by the AI safety filter. Please review the content.";
            }

            return generatedText?.Trim() ?? "Summary could not be generated.";
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(httpEx, "AI API rate limit reached (429)");
            return "Error: AI service rate limit reached (429). Please try again in a moment or check your API quota.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API");
            return "Error: Failed to generate summary from AI service.";
        }
    }
}
