using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jarvis.Core;

namespace Jarvis.Infrastructure;

public class OllamaLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    // Matches <think>...</think> blocks produced by DeepSeek-R1 and similar CoT models.
    // RegexOptions.Singleline ensures '.' matches newlines inside thinking blocks.
    private static readonly Regex ThinkTagRegex =
        new(@"<think>(.*?)</think>", RegexOptions.Singleline | RegexOptions.Compiled);

    public OllamaLLMService(string modelName = "qwen2.5:7b-instruct")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(5) // DeepSeek-R1 can take time for long reasoning
        };
        _modelName = modelName;
    }

    public async Task<LLMResponse> PromptAsync(string input)
    {
        try
        {
            var payload = new
            {
                model = _modelName,
                prompt = input,
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", payload);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
                return new LLMResponse("Error: Unexpected response format from Ollama.");

            var rawText = responseElement.GetString() ?? string.Empty;
            return ParseDeepSeekResponse(rawText);
        }
        catch (Exception ex)
        {
            return new LLMResponse($"Error communicating with Ollama: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts &lt;think&gt;...&lt;/think&gt; blocks from the raw model output.
    /// Returns a structured <see cref="LLMResponse"/> with the thinking process
    /// and the clean final answer separated.
    /// </summary>
    private static LLMResponse ParseDeepSeekResponse(string rawText)
    {
        var thinkMatches = ThinkTagRegex.Matches(rawText);

        if (thinkMatches.Count == 0)
            return new LLMResponse(rawText.Trim());

        // Collect all thinking blocks
        var thinkingParts = thinkMatches
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t));

        var thinking = string.Join("\n\n---\n\n", thinkingParts);

        // Strip ALL <think>...</think> blocks from the answer
        var cleanAnswer = ThinkTagRegex.Replace(rawText, string.Empty).Trim();

        return new LLMResponse(cleanAnswer, thinking);
    }
}
