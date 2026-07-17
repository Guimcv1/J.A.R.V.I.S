using System.Net.Http;
using System.Text.Json;

namespace Jarvis.Core;

public class GoogleSearchService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _cx;

    public GoogleSearchService(string apiKey, string cx)
    {
        _http = new HttpClient();
        _apiKey = apiKey;
        _cx = cx;
    }

    public async Task<List<SearchResult>> BuscarAsync(string query, int numResultados = 5)
    {
        var url = $"https://www.googleapis.com/customsearch/v1" +
                  $"?key={_apiKey}&cx={_cx}&q={Uri.EscapeDataString(query)}&num={numResultados}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var resultados = new List<SearchResult>();

        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                resultados.Add(new SearchResult
                {
                    Titulo = item.GetProperty("title").GetString() ?? "",
                    Link = item.GetProperty("link").GetString() ?? "",
                    Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : ""
                });
            }
        }

        return resultados;
    }
}

public class SearchResult
{
    public string Titulo { get; set; } = "";
    public string Link { get; set; } = "";
    public string Snippet { get; set; } = "";
}