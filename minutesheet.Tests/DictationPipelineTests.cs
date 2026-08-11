using Moq;
using minutesheet.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using minutesheet.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.IO;

namespace minutesheet.Tests;

public class DictationPipelineTests
{
    public DictationPipelineTests()
    {
    }

    [Fact]
    public void IsEnglishOnly_ShouldReturnTrue_ForEnglishText()
    {
        // Arrange
        var service = CreateServiceWithMocks(out _, out _, out _, out _);
        var method = typeof(DictationPipelineService).GetMethod("IsEnglishOnly", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (bool)method.Invoke(service, new object[] { "This is a standard English sentence." });

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEnglishOnly_ShouldReturnFalse_ForUrduText()
    {
        // Arrange
        var service = CreateServiceWithMocks(out _, out _, out _, out _);
        var method = typeof(DictationPipelineService).GetMethod("IsEnglishOnly", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (bool)method.Invoke(service, new object[] { "یہ ایک اردو جملہ ہے۔" });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEnglishOnly_ShouldReturnFalse_ForRomanUrdu()
    {
        // Arrange
        var service = CreateServiceWithMocks(out _, out _, out _, out _);
        var method = typeof(DictationPipelineService).GetMethod("IsEnglishOnly", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = (bool)method.Invoke(service, new object[] { "Minute sheet app sahi kaam nhi kr rhi hai" });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ProcessAudioAsync_ShouldSkipTranslation_WhenTextIsEnglishOnly()
    {
        // Arrange
        var service = CreateServiceWithMocks(out var mockGroq, out var mockLocal, out var mockCorrection, out var mockTranslation);
        
        var mockStream = new MemoryStream();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.OpenReadStream()).Returns(mockStream);
        mockFile.Setup(f => f.FileName).Returns("test.webm");

        mockGroq.Setup(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is english text.");
            
        mockCorrection.Setup(x => x.CorrectAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>>>()))
            .ReturnsAsync("This is english text."); // returns english only

        // Act
        var result = await service.ProcessAudioAsync(mockFile.Object, "en", CancellationToken.None);

        // Assert
        Assert.Equal("This is english text.", result);
        mockTranslation.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>>>()), Times.Never);
        mockLocal.Verify(x => x.TranscribeAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAudioAsync_ShouldCallTranslation_WhenTextIsRomanUrdu()
    {
        // Arrange
        var service = CreateServiceWithMocks(out var mockGroq, out var mockLocal, out var mockCorrection, out var mockTranslation);
        
        var mockStream = new MemoryStream();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.OpenReadStream()).Returns(mockStream);
        mockFile.Setup(f => f.FileName).Returns("test.webm");

        mockGroq.Setup(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Minute sheet app sahi kaam nhi kr rhi hai");
            
        mockCorrection.Setup(x => x.CorrectAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>>>()))
            .ReturnsAsync("Minute Sheet app sahi kaam nhi kr rhi hai");
            
        mockTranslation.Setup(x => x.TranslateAsync("Minute Sheet app sahi kaam nhi kr rhi hai", It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>>>()))
            .ReturnsAsync("Minute Sheet app is not working properly");

        // Act
        var result = await service.ProcessAudioAsync(mockFile.Object, "ur-PK", CancellationToken.None);

        // Assert
        Assert.Equal("Minute Sheet app is not working properly", result);
        mockTranslation.Verify(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<string>>>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAudioAsync_ShouldFallbackToLocalWhisper_WhenProviderIsLocal()
    {
        // Arrange
        var service = CreateServiceWithMocks(out var mockGroq, out var mockLocal, out var mockCorrection, out var mockTranslation, provider: "Local");
        
        var mockFile = new Mock<IFormFile>();

        mockLocal.Setup(x => x.TranscribeAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is local transcription text.");
            
        mockCorrection.Setup(x => x.CorrectAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>>>()))
            .ReturnsAsync("This is local transcription text.");

        // Act
        var result = await service.ProcessAudioAsync(mockFile.Object, "en", CancellationToken.None);

        // Assert
        Assert.Equal("This is local transcription text.", result);
        mockGroq.Verify(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAudioAsync_ShouldFallbackToLocalWhisper_WhenGroqThrows()
    {
        // Arrange
        var service = CreateServiceWithMocks(out var mockGroq, out var mockLocal, out var mockCorrection, out var mockTranslation);
        
        var mockStream = new MemoryStream();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.OpenReadStream()).Returns(mockStream);
        mockFile.Setup(f => f.FileName).Returns("test.webm");

        mockGroq.Setup(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Groq API error"));

        mockLocal.Setup(x => x.TranscribeAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Fallback text.");
            
        mockCorrection.Setup(x => x.CorrectAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<VocabularyCategory, IEnumerable<DomainVocabularyTerm>>>()))
            .ReturnsAsync("Fallback text.");

        // Act
        var result = await service.ProcessAudioAsync(mockFile.Object, "en", CancellationToken.None);

        // Assert
        Assert.Equal("Fallback text.", result);
        mockGroq.Verify(x => x.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockLocal.Verify(x => x.TranscribeAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private DictationPipelineService CreateServiceWithMocks(
        out Mock<IGroqTranscriptionService> mockGroq,
        out Mock<ILocalWhisperTranscriptionService> mockLocal,
        out Mock<ITranscriptCorrectionService> mockCorrection,
        out Mock<ITranslationService> mockTranslation,
        string provider = "Groq")
    {
        // Setup in-memory DB
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new ApplicationDbContext(options);

        mockGroq = new Mock<IGroqTranscriptionService>();
        mockLocal = new Mock<ILocalWhisperTranscriptionService>();
        mockCorrection = new Mock<ITranscriptCorrectionService>();
        mockTranslation = new Mock<ITranslationService>();

        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["Dictation:TranscriptionProvider"]).Returns(provider);

        return new DictationPipelineService(
            mockGroq.Object,
            mockLocal.Object,
            mockCorrection.Object,
            mockTranslation.Object,
            dbContext,
            mockConfig.Object,
            new Mock<ILogger<DictationPipelineService>>().Object
        );
    }
}
