namespace Jarvis.Core.Agent;

/// <summary>
/// Representa uma mensagem no histórico de conversa com o modelo.
///
/// Roles utilizados pela API do Ollama:
///   "user"      — mensagem do usuário
///   "assistant" — resposta do modelo
///   "tool"      — resultado de uma tool call retornado ao modelo
///   "system"    — instruções de sistema (opcional, enviadas no início)
/// </summary>
/// <param name="Role">Papel do autor da mensagem.</param>
/// <param name="Content">Conteúdo textual da mensagem.</param>
public record ChatMessage(string Role, string Content);
