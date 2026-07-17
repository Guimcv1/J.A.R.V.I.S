using System.Diagnostics;
using System.Text.Json.Nodes;
using Jarvis.Core.Tools;

namespace Jarvis.Tools.Pc;

/// <summary>
/// Ferramenta para abrir programas e aplicativos no computador.
///
/// Usa <see cref="Process.Start"/> — a forma padrão do .NET para lançar processos.
/// Funciona em Linux e Windows sem nenhum código nativo ou P/Invoke.
///
/// ─── Por que não precisamos de P/Invoke aqui? ───────────────────────────────
/// Process.Start() é uma abstração do .NET Runtime que internamente usa:
///   • No Linux : fork() + execve() (syscalls do kernel)
///   • No Windows: CreateProcess() (Win32 API)
/// O Runtime cuida disso por nós. P/Invoke só é necessário quando queremos
/// chamar funções da Win32 API que o .NET não expõe — como EnumWindows,
/// PostMessage, etc. (que veremos nas ferramentas de janelas).
/// ────────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class AbrirProgramaTool : IJarvisTool
{
    public string Name => "abrir_programa";

    public string Description =>
        "Abre um programa ou aplicativo no computador. " +
        "Aceita o nome do executável (ex: 'firefox', 'gedit', 'code') " +
        "ou o caminho completo (ex: '/usr/bin/vlc'). " +
        "Opcionalmente aceita argumentos como uma URL para o navegador.";

    public JsonObject ParameterSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["programa"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Nome do executável ou caminho completo (ex: 'firefox', 'gedit', '/usr/bin/vlc')"
            },
            ["argumentos"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Argumentos opcionais (ex: 'https://google.com' para abrir no navegador)"
            }
        },
        ["required"] = new JsonArray("programa")
    };

    public Task<string> ExecuteAsync(JsonObject arguments)
    {
        var programa = arguments["programa"]?.GetValue<string>()
            ?? throw new ArgumentException("Parâmetro 'programa' é obrigatório.");

        var argumentos = arguments["argumentos"]?.GetValue<string>() ?? "";

        // ProcessStartInfo configura como o processo será iniciado.
        var startInfo = new ProcessStartInfo
        {
            FileName = programa,
            Arguments = argumentos,

            // UseShellExecute = true:
            //   • No Linux: delega ao shell do ambiente (permite abrir por nome, sem caminho completo)
            //   • No Windows: delega ao ShellExecute() da Win32 API (abre documentos, URLs, etc.)
            // Com false, precisaríamos do caminho absoluto do executável.
            UseShellExecute = true,

            // Abre em background — não bloqueia o Jarvis esperando o app fechar
            CreateNoWindow = false,
        };

        try
        {
            var processo = Process.Start(startInfo);

            if (processo is null)
                return Task.FromResult($"Não foi possível iniciar '{programa}'. Verifique se está instalado.");

            return Task.FromResult(
                $"Programa '{programa}' iniciado com sucesso. PID: {processo.Id}.");
        }
        catch (Exception ex)
        {
            // Erros comuns: executável não encontrado, permissão negada
            return Task.FromResult($"Erro ao abrir '{programa}': {ex.Message}");
        }
    }
}
