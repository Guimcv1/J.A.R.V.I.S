namespace Jarvis.Core;

/// <summary>
/// Structured response from the local LLM.
/// Separates the internal "thinking" chain-of-thought from the final clean answer.
/// DeepSeek-R1 and similar models emit thinking inside &lt;think&gt;...&lt;/think&gt; tags.
/// </summary>
public record LLMResponse(
    string Answer,
    string? Thinking = null)
{
    /// <summary>True if this response contains a chain-of-thought block.</summary>
    public bool HasThinking => !string.IsNullOrWhiteSpace(Thinking);
}
