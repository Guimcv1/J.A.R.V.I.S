namespace Jarvis.Core;

public interface ILLMService
{
    Task<LLMResponse> PromptAsync(string input);
    Task UnloadModelAsync();
}

