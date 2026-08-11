using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using minutesheet.Data;

namespace minutesheet.Services;

public interface IDictationPipelineService
{
    Task<string> ProcessAudioAsync(Microsoft.AspNetCore.Http.IFormFile audio, string whisperLanguage, CancellationToken cancellationToken);
}

public class DictationPipelineService : IDictationPipelineService
{
    private readonly IGroqTranscriptionService _groqTranscriptionService;
    private readonly ILocalWhisperTranscriptionService _localTranscriptionService;
    private readonly ITranscriptCorrectionService _correctionService;
    private readonly ITranslationService _translationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DictationPipelineService> _logger;

    public DictationPipelineService(
        IGroqTranscriptionService groqTranscriptionService,
        ILocalWhisperTranscriptionService localTranscriptionService,
        ITranscriptCorrectionService correctionService,
        ITranslationService translationService,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<DictationPipelineService> logger)
    {
        _groqTranscriptionService = groqTranscriptionService;
        _localTranscriptionService = localTranscriptionService;
        _correctionService = correctionService;
        _translationService = translationService;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> ProcessAudioAsync(Microsoft.AspNetCore.Http.IFormFile audio, string whisperLanguage, CancellationToken cancellationToken)
    {
        // Step 1: Load Categorized Vocabulary
        var rawVocab = await _dbContext.DomainVocabularyTerms
            .Where(v => v.IsActive)
            .ToListAsync(cancellationToken);
            
        var categorizedVocab = rawVocab
            .GroupBy(v => v.Category)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var flatVocabTerms = rawVocab.Select(v => v.Term).ToList();

        // Step 2: Base Transcription
        var provider = _configuration["Dictation:TranscriptionProvider"] ?? "Groq";
        string transcript = string.Empty;

        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var audioStream = audio.OpenReadStream();
                transcript = await _groqTranscriptionService.TranscribeAsync(
                    audioStream,
                    audio.FileName,
                    flatVocabTerms,
                    whisperLanguage == "ur-PK" ? "ur" : null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Groq transcription failed. Falling back to local Whisper.");
                transcript = await _localTranscriptionService.TranscribeAsync(audio, whisperLanguage, cancellationToken);
            }
        }
        else
        {
            transcript = await _localTranscriptionService.TranscribeAsync(audio, whisperLanguage, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return transcript;
        }

        // Upstream safety guard: if the transcript is near-empty or just a few characters of noise, skip LLM processing
        var wordCount = transcript.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < 3)
        {
            _logger.LogInformation("Transcript is too short ({WordCount} words), skipping correction and translation.", wordCount);
            return transcript;
        }

        // Step 3: AI Transcript Correction
        var correctedTranscript = await _correctionService.CorrectAsync(transcript, categorizedVocab);

        // Step 4: Fast Language Classification (Heuristic)
        bool isEnglishOnly = IsEnglishOnly(correctedTranscript);
        _logger.LogInformation("Language classification heuristic result: IsEnglishOnly={IsEnglishOnly} for text: {Text}", isEnglishOnly, correctedTranscript);

        // Step 5: Conditional LLM Translation
        if (!isEnglishOnly)
        {
            var stringVocab = categorizedVocab.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(v => v.Term)
            );
            
            // Step 6: Mixed/Urdu Translation
            var finalEnglish = await _translationService.TranslateAsync(correctedTranscript, stringVocab);
        
            _logger.LogInformation("Pipeline complete. Final English length: {Length}", finalEnglish.Length);
        
            return finalEnglish;
        }

        // Step 6: Return Final English Text
        return correctedTranscript;
    }

    private bool IsEnglishOnly(string text)
    {
        // Simple heuristic: if the text contains any Urdu/Arabic script characters, it's not English only.
        // Urdu Unicode block is generally \u0600-\u06FF (Arabic) and some extensions.
        if (Regex.IsMatch(text, @"[\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFF]"))
        {
            return false;
        }

        // Basic Roman Urdu detection: look for common Roman Urdu words that are not standard English
        // This is a rudimentary list; a real implementation might use a dedicated language identification library
        var romanUrduKeywords = new[] { "hai", "hain", "aur", "ki", "ka", "ko", "mein", "se", "yeh", "woh", "kya", "nahi", "nhi", "kr", "kar", "rha", "rhi", "rhe" };
        
        var words = text.ToLowerInvariant().Split(new[] { ' ', '.', ',', '?', '!', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int romanUrduMatchCount = words.Count(w => romanUrduKeywords.Contains(w));

        // If we find multiple common Roman Urdu words, assume it's mixed or Roman Urdu
        if (romanUrduMatchCount >= 2)
        {
            return false;
        }

        return true;
    }
}
