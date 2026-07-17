using Jarvis.Agent;
using Jarvis.Core.Tools;
using Jarvis.Infrastructure;
using Jarvis.Tools.Pc;
using Jarvis.Tools.Web;

// ═══════════════════════════════════════════════════════════════════════════
//  CONFIGURAÇÃO
//  Lê variáveis de ambiente para não expor credenciais no código-fonte.
//  Configure no seu shell:
//    export GOOGLE_API_KEY="sua_chave_aqui"
//    export GOOGLE_CX="426d2d142e81b4b98"
//    export OLLAMA_MODEL="qwen2.5:7b-instruct"   (opcional)
// ═══════════════════════════════════════════════════════════════════════════

var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
var googleCx     = Environment.GetEnvironmentVariable("GOOGLE_CX") ?? "426d2d142e81b4b98";
var ollamaModel  = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5:7b-instruct";

// ─── Registro de ferramentas ──────────────────────────────────────────────
// Para adicionar uma nova ferramenta no futuro, basta incluí-la aqui.
// O agente a descobre automaticamente — nenhuma outra mudança necessária.

var ferramentas = new List<IJarvisTool>
{
    // ── Busca web ──
    new BuscarWebTool(googleApiKey, googleCx),

    // ── Controle de programas ──
    new AbrirProgramaTool(),

    // ── Gerenciamento de janelas (requer wmctrl: sudo apt install wmctrl) ──
    new ListarJanelasTool(),
    new FecharJanelaTool(),

    // ── Manipulação de arquivos (sandbox: ~/Jarvis/workspace/) ──
    new LerArquivoTool(),
    new EscreverArquivoTool(),
};

// ─── Serviços ────────────────────────────────────────────────────────────
var agente = new OllamaAgent(ferramentas, ollamaModel);
var tts = new PiperTtsService();

// ─── Banner ───────────────────────────────────────────────────────────────
Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("""
  ╔══════════════════════════════════════════════════════╗
  ║                                                      ║
  ║        J A R V I S  —  Assistente Local              ║
  ║                                                      ║
  ╚══════════════════════════════════════════════════════╝
  """);
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Modelo  : {ollamaModel}");
Console.WriteLine($"  Tools   : {string.Join(", ", ferramentas.Select(t => t.Name))}");
Console.WriteLine($"  Workspace: {LerArquivoTool.WorkspaceDir}");
Console.WriteLine();
Console.WriteLine("  Comandos especiais:");
Console.WriteLine("    sair   — encerra o Jarvis");
Console.WriteLine("    limpar — inicia uma nova sessão (apaga o histórico)");
Console.WriteLine("    tools  — lista as ferramentas disponíveis");
Console.ResetColor();
Console.WriteLine();

// ─── Loop de conversa ────────────────────────────────────────────────────
while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Você › ");
    Console.ResetColor();

    var entrada = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(entrada))
        continue;

    // Comandos locais (não passam pelo modelo)
    switch (entrada.ToLowerInvariant())
    {
        case "sair":
        case "exit":
        case "quit":
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("\nJarvis encerrado. Até logo!");
            Console.ResetColor();
            return;

        case "limpar":
        case "clear":
            agente.ClearHistory();
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("[Nova sessão iniciada — histórico apagado]\n");
            Console.ResetColor();
            continue;

        case "tools":
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\nFerramentas disponíveis:");
            foreach (var t in ferramentas)
                Console.WriteLine($"  • {t.Name}: {t.Description[..Math.Min(t.Description.Length, 80)]}...");
            Console.WriteLine();
            Console.ResetColor();
            continue;
    }

    // Indicador de processamento
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("\nJarvis › ");
    Console.ResetColor();

    try
    {
        var resposta = await agente.ChatAsync(entrada);

        // Move o cursor para depois dos logs de tool calls
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\nJarvis › ");
        Console.ResetColor();
        Console.WriteLine(resposta);

        // Dispara o TTS em background (fire and forget) para não travar o loop de chat
        _ = tts.SpeakAsync(resposta);
    }
    catch (HttpRequestException ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[Erro de conexão] Não foi possível contatar o Ollama: {ex.Message}");
        Console.WriteLine("Verifique se o Ollama está rodando: ollama serve");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[Erro inesperado] {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
}
