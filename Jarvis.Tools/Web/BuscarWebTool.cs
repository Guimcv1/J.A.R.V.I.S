using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Web;

/// <summary>
/// Ferramenta de busca na web via Google Custom Search API.
/// O modelo a usa quando precisar de informações em tempo real, notícias recentes,
/// ou qualquer dado que não esteja no seu treinamento.
/// </summary>
public sealed class BuscarWebTool : IJarvisTool
{
    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly string _cx; // Custom Search Engine ID

    public string Name => "buscar_web";

    public string Description =>
        "Busca informações atuais e em tempo real na internet via Google. " +
        "Use quando o usuário perguntar sobre notícias, eventos recentes, preços, " +
        "clima, resultados esportivos ou qualquer informação que possa ter mudado.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Termo ou pergunta a ser pesquisada no Google"
            }
        },
        ["required"] = new JsonArray("query")
    };

    public BuscarWebTool(string apiKey, string cx)
    {
        _apiKey = apiKey;
        _cx = cx;
    }

    public async Task<string> ExecuteAsync(JsonObject arguments)
    {
        var query = arguments["query"]?.GetValue<string>()
            ?? throw new ArgumentException("Parâmetro 'query' é obrigatório.");

        var url = "https://www.googleapis.com/customsearch/v1" +
                  $"?key={_apiKey}&cx={_cx}&q={Uri.EscapeDataString(query)}&num=5";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var resultados = new List<string>();

        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var titulo = item.GetProperty("title").GetString() ?? "";
                var link = item.GetProperty("link").GetString() ?? "";
                var snippet = item.TryGetProperty("snippet", out var s)
                    ? s.GetString() ?? ""
                    : "";

                resultados.Add($"• {titulo}\n  {snippet}\n  Fonte: {link}");
            }
        }

        return resultados.Count > 0
            ? string.Join("\n\n", resultados)
            : "Nenhum resultado encontrado para a busca.";
    }
}
