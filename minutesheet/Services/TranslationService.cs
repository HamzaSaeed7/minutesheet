using System.Text;
using minutesheet.Data;
using minutesheet.Services.OpenRouter;

namespace minutesheet.Services;

public interface ITranslationService
{
    Task<string> TranslateAsync(
        string text,
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>> vocabulary);
}

public class TranslationService : ITranslationService
{
    private readonly IOpenRouterClient _openRouterClient;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(IOpenRouterClient openRouterClient, ILogger<TranslationService> logger)
    {
        _openRouterClient = openRouterClient;
        _logger = logger;
    }

    public async Task<string> TranslateAsync(
        string text,
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>> vocabulary)
    {
        var systemPrompt = BuildSystemPrompt(vocabulary);

        var payload = new
        {
            model = "google/gemma-4-26b-a4b-it:free",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = text }
            }
        };

        // Use index 3 for Translation (as approved by the user)
        var responseText = await _openRouterClient.SendChatCompletionAsync(3, payload);

        if (responseText == null || responseText.StartsWith("Error:"))
        {
            _logger.LogWarning("Translation failed or returned an error. Using original text. Error: {Error}", responseText);
            return text; // Fallback to original text on failure
        }

        var resultText = responseText.Trim().Trim('"');
        if (LlmSafetyNet.LooksLikeMetaResponse(resultText))
        {
            _logger.LogWarning("LLM produced a meta-response instead of translating text. Response: {Response}", resultText);
            return text;
        }

        return resultText;
    }

    private static string BuildSystemPrompt(
        IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>> vocabulary)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            "You are translating dictated text for a workplace 'Minute Sheet' " +
            "application used to record meeting minutes, action items, and " +
            "departmental decisions for a Pakistani organization. The input may " +
            "be in English, Urdu script, Roman Urdu, or a mix of Urdu and " +
            "English in the same sentence — this code-switching is normal, not " +
            "an error.");
        sb.AppendLine();

        sb.AppendLine(
            "Convert the input into natural, professional English suitable for " +
            "an official workplace document. Do not translate word-by-word or " +
            "literally — preserve the intended meaning and tone.");
        sb.AppendLine();

        var vocabLines = vocabulary
            .Where(kv => kv.Value.Any())
            .Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}")
            .ToList();

        if (vocabLines.Count > 0)
        {
            sb.AppendLine(
                "Preserve the following known terms exactly as spelled below, " +
                "even if the input contains a misspelling or phonetic variant " +
                "of them:");
            foreach (var line in vocabLines)
            {
                sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        sb.AppendLine(
            "If a name or technical term is not on this list, still keep it " +
            "unchanged rather than translating or altering it.");
        sb.AppendLine();

        sb.AppendLine(
            "If the input is already fully in English, return it with only " +
            "minor cleanup of dictation artifacts (filler words, false starts) " +
            "— do not paraphrase or rewrite meaning.");
        sb.AppendLine();

        sb.AppendLine(
            "Even if the input is very short, fragmented, unclear, or appears to " +
            "contain transcription noise, you must still return your best-effort " +
            "corrected/translated version of it. Never ask a question, request " +
            "clarification, or comment on the quality of the input — always return " +
            "text, never a response addressed to the user.");
        sb.AppendLine();

        sb.AppendLine(
            "Respond ONLY with the final English text. No explanations, " +
            "labels, quotation marks, or commentary.");

        return sb.ToString();
    }
}