using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jarvis.Core.Agent;
using Jarvis.Core.Tools;

namespace Jarvis.Agent;

/// <summary>
/// Orquestrador central do Jarvis.
///
/// Implementa um loop de "agentic tool calling":
///   1. Envia mensagem do usuário ao Ollama junto com a lista de ferramentas disponíveis.
///   2. Se o modelo pedir uma ferramenta (tool_call), executa-a e devolve o resultado.
///   3. Repete até o modelo responder em linguagem natural, ou até MaxIteracoes tentativas.
///
/// Mantém o histórico completo da conversa em memória (memória de sessão),
/// para que o modelo "lembre" o que foi dito anteriormente.
/// </summary>
public sealed class OllamaAgent : IJarvisAgent
{
    private readonly HttpClient _http;
    private readonly IReadOnlyList<IJarvisTool> _tools;
    private readonly string _model;
    private readonly string _ollamaUrl;

    // Limite de iterações por mensagem para evitar loops infinitos de tool calls.
    private const int MaxIteracoes = 10;

    // Histórico de conversa: acumula mensagens de todas as trocas da sessão.
    // Cada entrada é um JsonObject com "role" e "content" (e opcionalmente "tool_calls").
    private readonly List<JsonNode> _historico = [];

    public OllamaAgent(
        IEnumerable<IJarvisTool> tools,
        string model = "qwen2.5:7b-instruct",
        string ollamaUrl = "http://localhost:11434/api/chat")
    {
        _tools = tools.ToList().AsReadOnly();
        _model = model;
        _ollamaUrl = ollamaUrl;

        _http = new HttpClient
        {
            // Timeout generoso: modelos locais podem demorar mais em respostas longas
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    /// <inheritdoc/>
    public void ClearHistory() => _historico.Clear();

    /// <inheritdoc/>
    public async Task<string> ChatAsync(string userMessage)
    {
        // Adiciona a mensagem do usuário ao histórico
        _historico.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = userMessage
        });

        for (int iteracao = 0; iteracao < MaxIteracoes; iteracao++)
        {
            var respostaJson = await EnviarParaOllamaAsync(_historico);
            using var doc = JsonDocument.Parse(respostaJson);
            var message = doc.RootElement.GetProperty("message");

            // --- Verifica se o modelo quer chamar ferramentas ---
            bool temToolCalls =
                message.TryGetProperty("tool_calls", out var toolCalls) &&
                toolCalls.GetArrayLength() > 0;

            if (!temToolCalls)
            {
                // Modelo respondeu em linguagem natural — fim do loop
                var resposta = message.GetProperty("content").GetString() ?? "";
                _historico.Add(new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = resposta
                });
                return resposta;
            }

            // --- Processa as tool calls ---
            // Preserva a mensagem "assistant" com tool_calls intacta no histórico
            // (o Ollama precisa receber isso de volta para manter o contexto correto)
            _historico.Add(JsonNode.Parse(message.GetRawText())!);

            foreach (var call in toolCalls.EnumerateArray())
            {
                var funcao = call.GetProperty("function");
                var nomeTool = funcao.GetProperty("name").GetString() ?? "";
                var argumentosEl = funcao.GetProperty("arguments");

                // Converte os argumentos do modelo para JsonObject para passar à ferramenta
                var argumentos = JsonNode.Parse(argumentosEl.GetRawText()) as JsonObject
                                 ?? new JsonObject();

                LogToolCall(nomeTool, argumentosEl.GetRawText());

                // Procura a ferramenta registrada com esse nome
                var tool = _tools.FirstOrDefault(t => t.Name == nomeTool);
                string resultado;

                if (tool is null)
                {
                    resultado = $"Erro: ferramenta '{nomeTool}' não encontrada. Ferramentas disponíveis: {string.Join(", ", _tools.Select(t => t.Name))}";
                }
                else
                {
                    try
                    {
                        resultado = await tool.ExecuteAsync(argumentos);
                    }
                    catch (Exception ex)
                    {
                        resultado = $"Erro ao executar '{nomeTool}': {ex.Message}";
                    }
                }

                LogToolResult(resultado);

                // Devolve o resultado da ferramenta ao histórico para a próxima iteração
                _historico.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["content"] = resultado
                });
            }

            // Volta ao início do loop para enviar o resultado ao modelo
        }

        // Chegou aqui: modelo ficou em loop de tool calls sem responder
        return "Jarvis: não consegui concluir a operação após várias tentativas.";
    }

    /// <summary>
    /// Constrói a lista de ferramentas no formato que o Ollama espera:
    /// [{ "type": "function", "function": { "name": ..., "description": ..., "parameters": ... } }]
    /// </summary>
    private JsonArray BuildToolDefinitions()
    {
        var array = new JsonArray();
        foreach (var tool in _tools)
        {
            array.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    // ParameterSchema já é um JsonObject — podemos cloná-lo diretamente
                    ["parameters"] = tool.ParameterSchema.DeepClone()
                }
            });
        }
        return array;
    }

    /// <summary>
    /// Envia o histórico de mensagens e as ferramentas ao Ollama via /api/chat
    /// e retorna o JSON bruto da resposta.
    /// </summary>
    private async Task<string> EnviarParaOllamaAsync(List<JsonNode> mensagens)
    {
        var payload = new JsonObject
        {
            ["model"] = _model,
            ["messages"] = JsonNode.Parse(JsonSerializer.Serialize(mensagens))!,
            ["tools"] = BuildToolDefinitions(),
            ["stream"] = false
        };

        var conteudo = new StringContent(
            payload.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var resposta = await _http.PostAsync(_ollamaUrl, conteudo);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadAsStringAsync();
    }

    // ── Helpers de log colorido no console ──────────────────────────────────

    private static void LogToolCall(string nome, string args)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write($"\n  ⚙ Tool call → {nome}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        // Trunca args longos para não poluir o terminal
        var argsLog = args.Length > 120 ? args[..120] + "…" : args;
        Console.WriteLine($"({argsLog})");
        Console.ResetColor();
    }

    private static void LogToolResult(string resultado)
    {
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write("  ✓ Resultado → ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var log = resultado.Length > 200 ? resultado[..200] + "…" : resultado;
        Console.WriteLine(log);
        Console.ResetColor();
    }
}
