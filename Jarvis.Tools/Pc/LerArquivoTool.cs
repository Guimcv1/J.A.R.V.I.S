using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Pc;

/// <summary>
/// Lê o conteúdo de um arquivo de texto do workspace do Jarvis.
///
/// Por segurança, só lê arquivos dentro do diretório sandbox:
///   ~/Jarvis/workspace/
///
/// Isso evita que o modelo leia arquivos sensíveis do sistema
/// (como ~/.ssh/id_rsa, /etc/passwd, etc.) por acidente.
/// </summary>
public sealed class LerArquivoTool : IJarvisTool
{
    /// <summary>Diretório sandbox onde os arquivos do Jarvis ficam.</summary>
    public static readonly string WorkspaceDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Jarvis", "workspace");

    public string Name => "ler_arquivo";

    public string Description =>
        $"Lê o conteúdo de um arquivo de texto do workspace do Jarvis ({WorkspaceDir}). " +
        "Use para ler anotações, listas, configurações ou qualquer arquivo de texto " +
        "que você pediu ao Jarvis para criar anteriormente.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["nome_arquivo"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = $"Nome do arquivo (ex: 'tarefas.txt', 'notas.md'). " +
                                  $"Sempre dentro de {WorkspaceDir}."
            }
        },
        ["required"] = new JsonArray("nome_arquivo")
    };

    public async Task<string> ExecuteAsync(JsonObject arguments)
    {
        var nomeArquivo = arguments["nome_arquivo"]?.GetValue<string>()
            ?? throw new ArgumentException("Parâmetro 'nome_arquivo' é obrigatório.");

        // Garante que o workspace existe
        Directory.CreateDirectory(WorkspaceDir);

        // Resolve o caminho absoluto e valida que está dentro do sandbox
        var caminho = ResolverCaminho(nomeArquivo);

        if (!File.Exists(caminho))
            return $"Arquivo '{nomeArquivo}' não encontrado no workspace. " +
                   $"Arquivos disponíveis: {ListarArquivos()}";

        var conteudo = await File.ReadAllTextAsync(caminho);

        if (string.IsNullOrWhiteSpace(conteudo))
            return $"Arquivo '{nomeArquivo}' está vazio.";

        return $"Conteúdo de '{nomeArquivo}':\n\n{conteudo}";
    }

    /// <summary>
    /// Resolve o caminho absoluto e garante que está dentro do WorkspaceDir.
    /// Previne ataques de path traversal (ex: "../../../etc/passwd").
    /// </summary>
    internal static string ResolverCaminho(string nomeArquivo)
    {
        // Path.GetFullPath resolve "..", "/" e outros truques de traversal
        var caminho = Path.GetFullPath(Path.Combine(WorkspaceDir, nomeArquivo));

        // Verifica que o caminho resolvido ainda está dentro do sandbox
        if (!caminho.StartsWith(WorkspaceDir, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"Acesso negado: só é permitido acessar arquivos em {WorkspaceDir}");

        return caminho;
    }

    private static string ListarArquivos()
    {
        if (!Directory.Exists(WorkspaceDir)) return "(nenhum)";
        var arquivos = Directory.GetFiles(WorkspaceDir).Select(Path.GetFileName);
        return string.Join(", ", arquivos) is { } lista && lista.Length > 0 ? lista : "(nenhum)";
    }
}
