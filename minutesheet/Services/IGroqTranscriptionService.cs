using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace minutesheet.Services;

public interface IGroqTranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName, IEnumerable<string> vocabulary, string? language = null, CancellationToken ct = default);
}
