using Xunit;
using minutesheet.Services;

namespace minutesheet.Tests;

public class LlmSafetyNetTests
{
    [Theory]
    [InlineData("Please provide the dictation text for processing. The current input appears to be fragmented or contains unrecognized characters.")]
    [InlineData("I cannot translate this text because it is too noisy.")]
    [InlineData("I'm unable to process the request.")]
    [InlineData("Could you clarify what you mean by that?")]
    [InlineData("This is a question?")]
    public void LooksLikeMetaResponse_ShouldReturnTrue_ForMetaResponses(string input)
    {
        var result = LlmSafetyNet.LooksLikeMetaResponse(input);
        Assert.True(result);
    }

    [Theory]
    [InlineData("Minute Sheet app is working correctly now.")]
    [InlineData("He said that the project is delayed.")]
    [InlineData("The system is up and running.")]
    public void LooksLikeMetaResponse_ShouldReturnFalse_ForLegitimateOutput(string input)
    {
        var result = LlmSafetyNet.LooksLikeMetaResponse(input);
        Assert.False(result);
    }
}
