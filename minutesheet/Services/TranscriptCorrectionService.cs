using System.Text;
using minutesheet.Data;
using minutesheet.Services.OpenRouter;

namespace minutesheet.Services;

public interface ITranscriptCorrectionService
{
    Task<string> CorrectAsync(
        string text,
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>> vocabulary);
}

public class TranscriptCorrectionService : ITranscriptCorrectionService
{
    private readonly IOpenRouterClient _openRouterClient;
    private readonly ILogger<TranscriptCorrectionService> _logger;

    public TranscriptCorrectionService(IOpenRouterClient openRouterClient, ILogger<TranscriptCorrectionService> logger)
    {
        _openRouterClient = openRouterClient;
        _logger = logger;
    }

    public async Task<string> CorrectAsync(
        string text,
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>> vocabulary)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var vocabContext = BuildVocabularyContext(vocabulary);

        if (string.IsNullOrEmpty(vocabContext))
        {
            // Nothing to correct against yet — skip the API call entirely.
            _logger.LogInformation("No active vocabulary terms found; skipping correction call.");
            return text;
        }

        var systemPrompt = BuildSystemPrompt(vocabContext);

        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = text }
            }
        };

        // Use index 1 for Correction
        var responseText = await _openRouterClient.SendChatCompletionAsync(1, payload);

        if (responseText == null || responseText.StartsWith("Error:"))
        {
            _logger.LogWarning("Correction failed or returned an error. Using original text. Error: {Error}", responseText);
            return text; // Fallback to original text on failure
        }

        var resultText = responseText.Trim().Trim('"');
        if (LlmSafetyNet.LooksLikeMetaResponse(resultText))
        {
            _logger.LogWarning("LLM produced a meta-response instead of correcting text. Response: {Response}", resultText);
            return text;
        }

        return resultText;
    }

    private static string BuildVocabularyContext(
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>> vocabulary)
    {
        var lines = vocabulary
            .Where(kv => kv.Value.Any())
            .Select(kv => $"{kv.Key}: " + string.Join(", ", kv.Value.Select(v =>
                v.Term + (string.IsNullOrWhiteSpace(v.Aliases) ? "" : $" (aliases: {v.Aliases})"))))
            .ToList();

        return string.Join("\n", lines);
    }

    private static string BuildSystemPrompt(string vocabContext)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            "You are a transcript correction assistant for a 'Minute Sheet' " +
            "application used for recording meeting minutes, action items, " +
            "and departmental decisions. You will be given a raw speech-to-text " +
            "transcript (which may be in English, Urdu, or Roman Urdu) and a " +
            "list of known correct domain terms, names, and abbreviations, " +
            "grouped by category, with common misheard variants noted in " +
            "parentheses where known.");
        sb.AppendLine();

        sb.AppendLine("Domain Vocabulary:");
        sb.AppendLine(vocabContext);
        sb.AppendLine();

        sb.AppendLine(
            "Replace a word in the transcript only if it is clearly a " +
            "phonetic mis-transcription or typo of a term on this list " +
            "(including its listed aliases). If you are not reasonably " +
            "confident a word matches a known term, leave it unchanged.");
        sb.AppendLine();

        sb.AppendLine(
            "Do not translate the text, do not change its meaning, and do " +
            "not alter grammar, punctuation, or word order beyond the " +
            "specific corrections described above.");
        sb.AppendLine();

        sb.AppendLine(
            "Even if the input is very short, fragmented, unclear, or appears to " +
            "contain transcription noise, you must still return your best-effort " +
            "corrected version of it. Never ask a question, request " +
            "clarification, or comment on the quality of the input — always return " +
            "text, never a response addressed to the user.");
        sb.AppendLine();

        sb.AppendLine("Respond ONLY with the corrected transcript. No explanations or commentary.");

        return sb.ToString();
    }
}