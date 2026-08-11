using System.Diagnostics;
using System.ComponentModel;

namespace minutesheet.Services;

public interface ILocalWhisperTranscriptionService
{
    Task<string> TranscribeAsync(IFormFile audio, string language, CancellationToken cancellationToken);
}

public class LocalWhisperTranscriptionService : ILocalWhisperTranscriptionService
{
    private const long MaxAudioBytes = 25 * 1024 * 1024;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalWhisperTranscriptionService> _logger;

    public LocalWhisperTranscriptionService(
        IConfiguration configuration,
        ILogger<LocalWhisperTranscriptionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(IFormFile audio, string language, CancellationToken cancellationToken)
    {
        if (audio.Length == 0 || audio.Length > MaxAudioBytes)
        {
            throw new ArgumentException("The audio recording must be between 1 byte and 25 MB.");
        }

        var executable = _configuration["LocalWhisper:Executable"]
            ?? Environment.GetEnvironmentVariable("LOCAL_WHISPER_EXECUTABLE")
            ?? "whisper";
        var model = _configuration["LocalWhisper:Model"]
            ?? Environment.GetEnvironmentVariable("LOCAL_WHISPER_MODEL")
            ?? "small";
        var workingDirectory = Path.Combine(Path.GetTempPath(), "minutesheet-whisper", Guid.NewGuid().ToString("N"));
        var inputExtension = Path.GetExtension(audio.FileName);
        if (string.IsNullOrWhiteSpace(inputExtension) || inputExtension.Length > 8)
        {
            inputExtension = ".webm";
        }

        Directory.CreateDirectory(workingDirectory);
        var inputPath = Path.Combine(workingDirectory, $"recording{inputExtension}");
        var outputPath = Path.Combine(workingDirectory, "recording.txt");

        try
        {
            await using (var input = File.Create(inputPath))
            await using (var source = audio.OpenReadStream())
            {
                await source.CopyToAsync(input, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(model);
            startInfo.ArgumentList.Add("--language");
            startInfo.ArgumentList.Add(language == "ur-PK" ? "ur" : "en");
            startInfo.ArgumentList.Add("--task");
            startInfo.ArgumentList.Add("transcribe");
            startInfo.ArgumentList.Add("--output_dir");
            startInfo.ArgumentList.Add(workingDirectory);
            startInfo.ArgumentList.Add("--output_format");
            startInfo.ArgumentList.Add("txt");
            startInfo.ArgumentList.Add("--fp16");
            startInfo.ArgumentList.Add("False");
            startInfo.ArgumentList.Add("--verbose");
            startInfo.ArgumentList.Add("False");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start the local Whisper executable.");
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }

            _ = await standardOutput;
            var error = await standardError;
            if (process.ExitCode != 0)
            {
                _logger.LogError("Local Whisper failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new InvalidOperationException("Local Whisper could not process this recording.");
            }

            return File.Exists(outputPath)
                ? (await File.ReadAllTextAsync(outputPath, cancellationToken)).Trim()
                : string.Empty;
        }
        catch (Win32Exception exception)
        {
            _logger.LogError(exception, "Local Whisper executable was not found: {Executable}", executable);
            throw new InvalidOperationException("Local Whisper is not available. Configure LocalWhisper:Executable with the Whisper command path.");
        }
        finally
        {
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Could not remove temporary Whisper files from {Directory}", workingDirectory);
            }
        }
    }
}
