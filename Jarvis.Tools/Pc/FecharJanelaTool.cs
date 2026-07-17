using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Pc;

/// <summary>
/// Fecha uma janela aberta no desktop (Linux).
///
/// Usa `wmctrl -c "Título"` para fechar graciosamente (equivale a clicar no X).
/// Ou `wmctrl -ic 0xID` para fechar pelo ID hexadecimal.
///
/// ─── Equivalente Windows (P/Invoke — explicação) ─────────────────────────────
/// No Windows, fecharíamos uma janela enviando a mensagem WM_CLOSE ao seu handle.
/// Toda ação do Windows sobre janelas é feita "enviando mensagens":
///
///   [DllImport("user32.dll")]
///   private static extern bool PostMessage(
///       IntPtr hWnd,    // handle da janela alvo (obtido via EnumWindows)
///       uint Msg,       // ID da mensagem (WM_CLOSE = 0x0010)
///       IntPtr wParam,  // parâmetro extra (0 para WM_CLOSE)
///       IntPtr lParam); // parâmetro extra (0 para WM_CLOSE)
///
///   // Uso: PostMessage(hJanela, 0x0010, IntPtr.Zero, IntPtr.Zero);
///
/// A diferença entre PostMessage e SendMessage:
///   • PostMessage: coloca a mensagem na fila e retorna imediatamente (assíncrono)
///   • SendMessage: aguarda o processamento completo da mensagem (síncrono)
///
/// Para fechar forçadamente (sem perguntar "deseja salvar?"):
///   [DllImport("user32.dll")]
///   private static extern bool DestroyWindow(IntPtr hWnd); // mais agressivo
/// ────────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class FecharJanelaTool : IJarvisTool
{
    public string Name => "fechar_janela";

    public string Description =>
        "Fecha uma janela aberta no desktop. " +
        "Prefira usar o ID hexadecimal (obtido via listar_janelas) para precisão, " +
        "mas também aceita parte do título da janela.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["titulo"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Parte do título da janela a fechar (ex: 'Firefox', 'gedit')"
            },
            ["id"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "ID hexadecimal da janela (ex: '0x03400003'), mais preciso que o título"
            }
        }
        // Nenhum parâmetro obrigatório — mas pelo menos um dos dois deve ser informado
    };

    public async Task<string> ExecuteAsync(JsonObject arguments)
    {
        var id = arguments["id"]?.GetValue<string>();
        var titulo = arguments["titulo"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(titulo))
            return "Erro: informe pelo menos o 'id' ou o 'titulo' da janela.";

        string comando, args;

        if (!string.IsNullOrWhiteSpace(id))
        {
            // Fechar pelo ID é mais preciso: wmctrl -ic 0x1234abcd
            // "-i" indica que o próximo argumento é um ID (não título)
            // "-c" = close (fecha graciosamente via WM_DELETE_WINDOW)
            comando = "wmctrl";
            args = $"-ic {id}";
        }
        else
        {
            // Fechar pelo título: wmctrl -c "Firefox"
            // wmctrl faz match parcial, case-insensitive
            comando = "wmctrl";
            args = $"-c \"{titulo}\"";
        }

        try
        {
            await ListarJanelasTool.ExecutarComandoAsync(comando, args);
            var alvo = !string.IsNullOrWhiteSpace(id) ? $"ID {id}" : $"'{titulo}'";
            return $"Janela {alvo} fechada com sucesso.";
        }
        catch (Exception ex)
        {
            return $"Erro ao fechar janela: {ex.Message}. " +
                   "Verifique se wmctrl está instalado: sudo apt install wmctrl";
        }
    }
}
