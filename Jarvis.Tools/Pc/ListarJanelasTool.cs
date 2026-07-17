using System.Diagnostics;
using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Pc;

/// <summary>
/// Lista todas as janelas abertas e visíveis no desktop (Linux).
///
/// Implementação Linux: usa `wmctrl -l` — um utilitário de linha de comando
/// que interage com o gerenciador de janelas via protocolo EWMH/NetWM.
///
/// ─── Equivalente Windows (P/Invoke — explicação) ─────────────────────────────
/// No Windows, para listar janelas precisaríamos chamar a função EnumWindows()
/// da Win32 API, que não existe no .NET. Faríamos assim com P/Invoke:
///
///   // P/Invoke declara uma função nativa como se fosse C#
///   // DllImport diz ao Runtime: "essa função está em user32.dll"
///   [DllImport("user32.dll")]
///   private static extern bool EnumWindows(
///       EnumWindowsProc lpEnumFunc,  // callback chamado para cada janela
///       IntPtr lParam);              // parâmetro arbitrário
///
///   // Delegate que representa o tipo do callback
///   private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
///
///   // Para obter o título de uma janela pelo seu handle (hWnd):
///   [DllImport("user32.dll", CharSet = CharSet.Unicode)]
///   private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
///
///   // Para verificar se uma janela está visível (filtramos as invisíveis):
///   [DllImport("user32.dll")]
///   private static extern bool IsWindowVisible(IntPtr hWnd);
///
/// IntPtr é o tipo C# para "ponteiro/handle opaco" — equivale a HWND, HANDLE, etc.
/// Veremos isso em detalhes quando implementarmos mouse/teclado no Windows.
/// ────────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class ListarJanelasTool : IJarvisTool
{
    public string Name => "listar_janelas";

    public string Description =>
        "Lista todas as janelas abertas no desktop. " +
        "Retorna o ID e o título de cada janela. " +
        "Use antes de fechar uma janela para saber o ID correto.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),  // sem parâmetros
        ["required"] = new JsonArray()
    };

    public async Task<string> ExecuteAsync(JsonObject arguments)
    {
        // wmctrl -l lista janelas no formato:
        //   0x03400003  0  hostname  Título da Janela
        //   ^-- ID hex  ^desktop ^hostname  ^título
        var resultado = await ExecutarComandoAsync("wmctrl", "-l");

        if (string.IsNullOrWhiteSpace(resultado))
            return "Nenhuma janela encontrada (wmctrl retornou vazio). " +
                   "Verifique se wmctrl está instalado: sudo apt install wmctrl";

        // Parseia cada linha e formata de forma mais legível
        var linhas = resultado
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParsearLinhaWmctrl)
            .Where(j => !string.IsNullOrWhiteSpace(j.Titulo))
            .ToList();

        if (!linhas.Any())
            return "Nenhuma janela visível encontrada.";

        var lista = linhas.Select(j => $"  ID: {j.Id}  |  {j.Titulo}");
        return $"Janelas abertas ({linhas.Count}):\n" + string.Join("\n", lista);
    }

    /// <summary>Parseia uma linha de saída do wmctrl -l.</summary>
    private static (string Id, string Titulo) ParsearLinhaWmctrl(string linha)
    {
        // Formato: "0x03400003  0  hostname  Título Completo"
        // Dividimos nos espaços, mas o título pode ter espaços — usamos split com limit
        var partes = linha.Split([' ', '\t'], 4, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 4) return (partes.FirstOrDefault() ?? "", "");

        return (partes[0], partes[3]);
    }

    internal static async Task<string> ExecutarComandoAsync(string comando, string args)
    {
        var psi = new ProcessStartInfo(comando, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var processo = Process.Start(psi)
            ?? throw new InvalidOperationException($"Não foi possível iniciar '{comando}'.");

        var saida = await processo.StandardOutput.ReadToEndAsync();
        await processo.WaitForExitAsync();
        return saida;
    }
}
