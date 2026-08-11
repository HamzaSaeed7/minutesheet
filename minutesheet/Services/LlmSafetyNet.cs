namespace minutesheet.Services;

public static class LlmSafetyNet
{
    public static bool LooksLikeMetaResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        var lower = response.ToLowerInvariant();
        return lower.Contains("please provide") ||
               lower.Contains("i cannot") ||
               lower.Contains("i'm unable") ||
               lower.Contains("could you clarify") ||
               response.TrimEnd().EndsWith("?");
    }
}
