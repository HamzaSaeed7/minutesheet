using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace minutesheet.Services;

public class GroqTranscriptionService : IGroqTranscriptionService
{
    private const long MaxAudioBytes = 25 * 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GroqTranscriptionService> _logger;

    public GroqTranscriptionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GroqTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(
        Stream audioStream, 
        string fileName, 
        IEnumerable<string> vocabulary, 
        string? language = null, 
        CancellationToken ct = default)
    {
        if (audioStream.Length == 0 || audioStream.Length > MaxAudioBytes)
        {
            throw new ArgumentException("The audio recording must be between 1 byte and 25 MB for Groq API.");
        }

        var apiKey = _configuration["ApiKeys:Groq"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Groq API Key is not configured.");
            throw new InvalidOperationException("Groq API is not configured.");
        }

        var url = "https://api.groq.com/openai/v1/audio/transcriptions";

        // Truncate vocabulary to stay under 224 tokens. ~150 words is safe.
        var promptTerms = vocabulary.Take(150);
        var prompt = string.Join(", ", promptTerms);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        
        var streamContent = new StreamContent(audioStream);
        // Groq API uses standard content types, application/octet-stream is generally safe, or let HttpClient figure it out
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        content.Add(new StringContent("whisper-large-v3"), "model");
        content.Add(new StringContent("json"), "response_format");
        
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            content.Add(new StringContent(prompt), "prompt");
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            content.Add(new StringContent(language), "language");
        }

        request.Content = content;

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Groq API error. Status: {StatusCode}, Body: {Body}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Groq API returned {(int)response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            
            if (doc.RootElement.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? string.Empty;
            }

            _logger.LogWarning("Groq API response missing 'text' field: {ResponseJson}", responseJson);
            return string.Empty;
        }
        catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(httpEx, "Groq API rate limit reached (429)");
            throw new InvalidOperationException("Groq API rate limit reached. Please try again later.", httpEx);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to call Groq API");
            throw new InvalidOperationException($"Failed to interact with Groq service. {ex.Message}", ex);
        }
    }
}
