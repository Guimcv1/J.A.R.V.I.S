using System.Text.Json.Nodes;

namespace Jarvis.Core.Tools;

/// <summary>
/// Contrato que toda ferramenta do Jarvis deve implementar.
///
/// O orquestrador (<see cref="Jarvis.Core.Agent.IJarvisAgent"/>) descobre as ferramentas
/// disponíveis através desta interface e as expõe automaticamente ao modelo de linguagem.
///
/// Para adicionar uma nova ferramenta:
///   1. Crie uma classe em Jarvis.Tools que implemente IJarvisTool
///   2. Registre-a no Jarvis.Console/Program.cs
///   Pronto — o modelo já pode chamá-la sem nenhuma outra mudança.
/// </summary>
public interface IJarvisTool
{
    /// <summary>
    /// Nome da ferramenta em snake_case. Este é o identificador que o modelo usa
    /// para chamá-la (ex: "abrir_programa", "buscar_web").
    /// Deve ser único entre todas as ferramentas registradas.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Descrição em linguagem natural enviada ao modelo para que ele saiba
    /// QUANDO usar esta ferramenta. Seja específico e direto.
    /// Ex: "Abre um programa instalado no computador pelo nome do executável."
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema descrevendo os parâmetros desta ferramenta.
    /// O modelo usará este schema para saber quais argumentos passar.
    ///
    /// Exemplo mínimo:
    /// <code>
    /// new JsonObject
    /// {
    ///     ["type"] = "object",
    ///     ["properties"] = new JsonObject
    ///     {
    ///         ["query"] = new JsonObject { ["type"] = "string", ["description"] = "..." }
    ///     },
    ///     ["required"] = new JsonArray("query")
    /// }
    /// </code>
    /// </summary>
    JsonObject ParameterSchema { get; }

    /// <summary>
    /// Executa a ferramenta com os argumentos fornecidos pelo modelo.
    /// Deve retornar uma string com o resultado (que o modelo usará para responder ao usuário).
    /// Em caso de erro, retorne uma mensagem descritiva — não lance exceções.
    /// </summary>
    Task<string> ExecuteAsync(JsonObject arguments);
}
