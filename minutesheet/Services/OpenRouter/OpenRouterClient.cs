using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace minutesheet.Services.OpenRouter;

public class OpenRouterClient : IOpenRouterClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetApiKey(int index)
    {
        var key = _configuration[$"OpenRouterSettings:ApiKeys:{index}"];
        if (!string.IsNullOrWhiteSpace(key)) return key;
        
        // Fallback to legacy single-key format if array is missing
        return _configuration["OpenRouterSettings:ApiKey"] ?? _configuration["GeminiSettings:ApiKey"] ?? string.Empty;
    }

    public async Task<string?> SendChatCompletionAsync(int apiKeyIndex, object payload)
    {
        var apiKey = GetApiKey(apiKeyIndex);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("AI API Key is not configured for index {Index}.", apiKeyIndex);
            return null;
        }

        var url = "https://openrouter.ai/api/v1/chat/completions";
        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

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
            _logger.LogInformation("OpenRouter raw response (index {Index}): {ResponseJson}", apiKeyIndex, responseJson);
            
            using var doc = JsonDocument.Parse(responseJson);
            
            var choice = doc.RootElement.GetProperty("choices")[0];
            var generatedText = choice.GetProperty("message").GetProperty("content").GetString();

            if (choice.TryGetProperty("finish_reason", out var finishReasonProp))
            {
                var finishReason = finishReasonProp.GetString();
                if (finishReason == "content_filter")
                {
                    _logger.LogWarning("Content was flagged by the AI safety filter.");
                    return "Error: The document text was flagged by the AI safety filter. Please review the content.";
                }
            }

            // Fallback check in case the model returns safety strings directly in the content
            if (!string.IsNullOrWhiteSpace(generatedText) && generatedText.Trim().StartsWith("User Safety:"))
            {
                _logger.LogWarning("Content was flagged by the AI safety filter (fallback).");
                return "Error: The document text was flagged by the AI safety filter. Please review the content.";
            }

            return generatedText;
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(httpEx, "AI API rate limit reached (429)");
            return "Error: AI service rate limit reached (429). Please try again in a moment or check your API quota.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI API");
            return $"Error: Failed to interact with AI service. {ex.Message}";
        }
    }
}
