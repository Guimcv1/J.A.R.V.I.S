namespace Jarvis.Core.Agent;

/// <summary>
/// Contrato do orquestrador central do Jarvis.
///
/// O agente recebe mensagens do usuário, coordena chamadas ao LLM (Ollama),
/// executa ferramentas quando o modelo pede, e retorna a resposta final.
///
/// Mantém histórico de conversa em memória durante a sessão — o modelo
/// "lembra" o que foi dito anteriormente na mesma sessão.
/// </summary>
public interface IJarvisAgent
{
    /// <summary>
    /// Envia uma mensagem do usuário ao agente e retorna a resposta do Jarvis.
    /// O histórico de conversa é mantido automaticamente entre chamadas.
    /// </summary>
    Task<string> ChatAsync(string userMessage);

    /// <summary>
    /// Limpa o histórico de conversa, iniciando uma nova sessão.
    /// Útil quando o usuário quer mudar de assunto ou a conversa ficou longa.
    /// </summary>
    void ClearHistory();
}
