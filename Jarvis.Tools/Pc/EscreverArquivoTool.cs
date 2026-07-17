using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Pc;

/// <summary>
/// Escreve ou sobrescreve o conteúdo de um arquivo de texto no workspace do Jarvis.
///
/// Workspace sandbox: ~/Jarvis/workspace/
/// Suporta modo "append" para adicionar ao final do arquivo sem perder o conteúdo existente.
/// </summary>
public sealed class EscreverArquivoTool : IJarvisTool
{
    public string Name => "escrever_arquivo";

    public string Description =>
        $"Cria ou sobrescreve um arquivo de texto no workspace do Jarvis ({LerArquivoTool.WorkspaceDir}). " +
        "Use para salvar anotações, listas de tarefas, código gerado, resumos, etc. " +
        "Com 'modo=append', adiciona ao final sem apagar o conteúdo existente.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["nome_arquivo"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Nome do arquivo (ex: 'tarefas.txt', 'notas.md')"
            },
            ["conteudo"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Texto a escrever no arquivo"
            },
            ["modo"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("criar", "append"),
                ["description"] = "'criar' (padrão) substitui o arquivo; 'append' adiciona ao final"
            }
        },
        ["required"] = new JsonArray("nome_arquivo", "conteudo")
    };

    public async Task<string> ExecuteAsync(JsonObject arguments)
    {
        var nomeArquivo = arguments["nome_arquivo"]?.GetValue<string>()
            ?? throw new ArgumentException("Parâmetro 'nome_arquivo' é obrigatório.");

        var conteudo = arguments["conteudo"]?.GetValue<string>()
            ?? throw new ArgumentException("Parâmetro 'conteudo' é obrigatório.");

        var modo = arguments["modo"]?.GetValue<string>() ?? "criar";

        // Garante que o workspace existe
        Directory.CreateDirectory(LerArquivoTool.WorkspaceDir);

        var caminho = LerArquivoTool.ResolverCaminho(nomeArquivo);

        if (modo == "append")
        {
            // Adiciona ao final — útil para logs ou listas crescentes
            await File.AppendAllTextAsync(caminho, conteudo + Environment.NewLine);
            return $"Conteúdo adicionado ao final de '{nomeArquivo}'. " +
                   $"Tamanho atual: {new FileInfo(caminho).Length} bytes.";
        }
        else
        {
            // Cria ou sobrescreve completamente
            await File.WriteAllTextAsync(caminho, conteudo);
            return $"Arquivo '{nomeArquivo}' salvo com sucesso em {LerArquivoTool.WorkspaceDir}. " +
                   $"Tamanho: {new FileInfo(caminho).Length} bytes.";
        }
    }
}
