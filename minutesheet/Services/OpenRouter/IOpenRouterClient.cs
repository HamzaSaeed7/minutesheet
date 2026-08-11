namespace minutesheet.Services.OpenRouter;

public interface IOpenRouterClient
{
    Task<string?> SendChatCompletionAsync(int apiKeyIndex, object payload);
}
