using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Jarvis.Core;

namespace Jarvis.Infrastructure;

/// <summary>
/// TTS local de alta qualidade usando Piper (https://github.com/rhasspy/piper).
///
/// ── Por que Piper? ────────────────────────────────────────────────────────────
/// Piper é um sintetizador de voz neural leve, 100% offline e multiplataforma.
/// Produz vozes muito mais naturais que espeak-ng, com latência baixa (~200ms)
/// e sem dependência de GPU (roda bem em CPU).
///
/// ── Cross-platform ────────────────────────────────────────────────────────────
/// Esta classe detecta o SO em runtime e escolhe:
///   • Binário Piper: piper  (Linux/Mac) ou piper.exe (Windows)
///   • Player de áudio: aplay (Linux)    ou ffplay     (Windows)
///
/// A interface pública (IAudioOutputService) é idêntica nos dois SOs —
/// quem usa esta classe não precisa saber nada sobre o SO subjacente.
///
/// ── Estrutura de arquivos esperada ────────────────────────────────────────────
/// {piperDirectory}/
///   ├── piper              ← binário Linux
///   ├── piper.exe          ← binário Windows (opcional)
///   └── models/
///       ├── en_GB-alan-medium.onnx
///       └── en_GB-alan-medium.onnx.json
///
/// Use o script scripts/setup-piper.sh (Linux) ou setup-piper.ps1 (Windows)
/// para baixar tudo automaticamente.
///
/// ── Fallback ─────────────────────────────────────────────────────────────────
/// Se o Piper não estiver instalado, cai automaticamente para espeak-ng
/// (se disponível) — o serviço nunca joga exceção pro chamador.
/// </summary>
public sealed class PiperTtsService : IAudioOutputService
{
    // ── Configuração resolvida no construtor ─────────────────────────────────

    private readonly string _piperExecutable;
    private string _modelPath;
    private readonly string _audioPlayer;
    private readonly string _audioPlayerArgsFormat; // {0} = path do WAV
    private readonly string _tempWavPath;
    private bool _isAvailable;

    // ── Estado de execução ───────────────────────────────────────────────────

    private Process? _activeProcess;
    private readonly object _lock = new();

    // ── Regex de sanitização (compilados uma vez, reusados) ─────────────────

    private static readonly Regex RxCodeBlock  = new(@"```[\s\S]*?```",       RegexOptions.Compiled);
    private static readonly Regex RxInlineCode = new(@"`[^`]+`",              RegexOptions.Compiled);
    private static readonly Regex RxHtmlTag    = new(@"<[^>]+>",              RegexOptions.Compiled);
    private static readonly Regex RxBoldItalic = new(@"\*{1,3}([^*]+)\*{1,3}", RegexOptions.Compiled);
    private static readonly Regex RxUnderline  = new(@"_{1,2}([^_]+)_{1,2}",  RegexOptions.Compiled);
    private static readonly Regex RxHeader     = new(@"^#{1,6}\s+",           RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex RxWhitespace = new(@"\s{2,}",               RegexOptions.Compiled);

    // ────────────────────────────────────────────────────────────────────────

    /// <param name="piperDirectory">
    ///   Pasta onde estão o binário e a pasta models/.
    ///   Se null, usa a pasta "piper/" ao lado do executável do Jarvis.
    /// </param>
    /// <param name="modelFileName">
    ///   Nome do arquivo .onnx a usar. Padrão: en_GB-alan-medium.onnx
    ///   (voz masculina britânica, grave e formal — estilo "mordomo").
    ///
    ///   Outras opções disponíveis no Piper:
    ///     • en_GB-northern_english_male-medium.onnx  (sotaque norte-inglês)
    ///     • en_US-ryan-high.onnx                     (masculino americano, alta qualidade)
    /// </param>
    public PiperTtsService(string? piperDirectory = null, string modelFileName = "pt_BR-faber-medium.onnx")
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        piperDirectory ??= Path.Combine(AppContext.BaseDirectory, "piper");

        // Se não encontrou no diretório atual (ex: rodando Jarvis.UI no modo de dev), tenta buscar no Jarvis.Console
        if (!Directory.Exists(piperDirectory))
        {
            var devConsolePiperDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Jarvis.Console", "bin", "Debug", "net10.0", "piper"));
            if (Directory.Exists(devConsolePiperDir))
            {
                piperDirectory = devConsolePiperDir;
            }
        }

        _piperExecutable = Path.Combine(piperDirectory, isWindows ? "piper.exe" : "piper");
        _modelPath       = Path.Combine(piperDirectory, "models", modelFileName);

        // ── Player de áudio ──────────────────────────────────────────────────
        // Linux: aplay faz parte do pacote alsa-utils, presente em quase todo desktop.
        //        Em sistemas com PipeWire (Arch, Fedora moderno), funciona via camada
        //        de compatibilidade ALSA — nenhuma configuração extra.
        //
        // Windows: ffplay.exe faz parte do ffmpeg (https://ffmpeg.org/download.html).
        //          Alternativa: instale via winget: winget install ffmpeg
        if (isWindows)
        {
            _audioPlayer           = "ffplay";
            _audioPlayerArgsFormat = "-autoexit -nodisp -loglevel quiet \"{0}\"";
        }
        else
        {
            _audioPlayer           = "aplay";
            _audioPlayerArgsFormat = "\"{0}\"";
        }

        // Arquivo WAV temporário — incluímos o PID para evitar colisão se houver
        // múltiplas instâncias do Jarvis rodando ao mesmo tempo.
        _tempWavPath = Path.Combine(
            Path.GetTempPath(),
            $"jarvis_tts_{Environment.ProcessId}.wav");

        // Checagem suave: avisa mas não joga exceção — permite fallback.
        _isAvailable = File.Exists(_piperExecutable) && File.Exists(_modelPath);

        if (!_isAvailable)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine(
                "[Piper TTS] Binário ou modelo não encontrado — usando espeak-ng como fallback.\n" +
                $"  Binário esperado : {_piperExecutable}\n" +
                $"  Modelo esperado  : {_modelPath}\n" +
                "  Execute scripts/setup-piper.sh para instalar automaticamente.");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Troca dinamicamente o modelo de voz do Piper TTS.
    /// </summary>
    public void SetLanguage(string modelFileName)
    {
        var dir = Path.GetDirectoryName(_modelPath);
        if (dir != null)
        {
            _modelPath = Path.Combine(dir, modelFileName);
            _isAvailable = File.Exists(_piperExecutable) && File.Exists(_modelPath);
            
            if (!_isAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"[Piper TTS] Modelo não encontrado para novo idioma: {_modelPath}");
                Console.ResetColor();
            }
        }
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string text)
    {
        // Interrompe qualquer fala em andamento antes de começar
        Stop();

        var clean = Sanitize(text);
        if (string.IsNullOrWhiteSpace(clean)) return;

        if (_isAvailable)
        {
            await SpeakWithPiperAsync(clean);
        }
        else
        {
            // Fallback: espeak-ng (qualidade pior, mas não trava o sistema)
            await SpeakWithEspeakFallbackAsync(clean);
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_lock)
        {
            try { _activeProcess?.Kill(entireProcessTree: true); }
            catch { /* processo já encerrou */ }
            _activeProcess = null;
        }
        TryDeleteTempWav();
    }

    // ── Implementação principal (Piper) ──────────────────────────────────────

    private async Task SpeakWithPiperAsync(string text)
    {
        try
        {
            await GerarWavComPiperAsync(text);

            if (File.Exists(_tempWavPath))
                await ReproduirWavAsync(_tempWavPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Piper TTS] Erro: {ex.Message}");
            // Tenta fallback silencioso para não deixar o Jarvis mudo
            await SpeakWithEspeakFallbackAsync(text);
        }
        finally
        {
            TryDeleteTempWav();
        }
    }

    /// <summary>
    /// Chama o Piper passando o texto via stdin e obtendo um WAV em disco.
    ///
    /// Formato do comando:
    ///   echo "texto" | piper --model voice.onnx --output_file out.wav
    ///
    /// Por que stdin em vez de argumento?
    ///   Textos com aspas, caracteres especiais ou muito longos quebram quando
    ///   passados como argumento de linha de comando. Stdin é mais seguro e sem limite.
    /// </summary>
    private async Task GerarWavComPiperAsync(string text)
    {
        var psi = new ProcessStartInfo(_piperExecutable)
        {
            Arguments            = $"--model \"{_modelPath}\" --output_file \"{_tempWavPath}\"",
            UseShellExecute      = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true, // capturamos para não poluir o console
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        Process? proc;
        lock (_lock)
        {
            proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Não foi possível iniciar o Piper.");
            _activeProcess = proc;
        }

        try
        {
            // Envia o texto e fecha o stdin para que o Piper saiba que acabou
            await proc.StandardInput.WriteLineAsync(text);
            proc.StandardInput.Close();

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"Piper retornou exit code {proc.ExitCode}: {err.Trim()}");
            }
        }
        finally
        {
            lock (_lock)
            {
                if (_activeProcess == proc) _activeProcess = null;
            }
        }
    }

    /// <summary>
    /// Reproduz o arquivo WAV usando o player adequado ao SO.
    /// Linux: aplay — Windows: ffplay
    /// </summary>
    private async Task ReproduirWavAsync(string wavPath)
    {
        var args = string.Format(_audioPlayerArgsFormat, wavPath);

        var psi = new ProcessStartInfo(_audioPlayer, args)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        Process? proc;
        lock (_lock)
        {
            proc = Process.Start(psi);
            _activeProcess = proc;
        }

        if (proc == null) return;

        try
        {
            await proc.WaitForExitAsync();
        }
        finally
        {
            lock (_lock)
            {
                if (_activeProcess == proc) _activeProcess = null;
            }
        }
    }

    // ── Fallback: espeak-ng ──────────────────────────────────────────────────

    /// <summary>
    /// Fallback de emergência usando espeak-ng.
    /// Qualidade inferior, mas garante que o Jarvis continue falando se o Piper falhar.
    /// Silencioso se o espeak-ng também não estiver instalado.
    /// </summary>
    private async Task SpeakWithEspeakFallbackAsync(string text)
    {
        try
        {
            var psi = new ProcessStartInfo("espeak-ng")
            {
                Arguments            = "-v en-gb+m3 -p 40 -s 140 --stdin",
                UseShellExecute      = false,
                RedirectStandardInput  = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            var proc = Process.Start(psi);
            if (proc == null) return;

            await proc.StandardInput.WriteLineAsync(text);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync();
        }
        catch
        {
            // espeak-ng também não disponível — silêncio intencional, sem crash
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void TryDeleteTempWav()
    {
        try { if (File.Exists(_tempWavPath)) File.Delete(_tempWavPath); }
        catch { /* arquivo ainda em uso, será deletado na próxima chamada */ }
    }

    /// <summary>
    /// Remove markdown, código, HTML e formatações antes de sintetizar.
    /// Texto limpo = fala mais natural e sem artefatos.
    /// </summary>
    private static string Sanitize(string text)
    {
        text = RxCodeBlock.Replace(text, " ");         // ```código```
        text = RxInlineCode.Replace(text, " ");        // `código`
        text = RxHtmlTag.Replace(text, " ");           // <think>, <b>, etc.
        text = RxBoldItalic.Replace(text, "$1");       // **negrito**, *itálico*
        text = RxUnderline.Replace(text, "$1");        // __sublinhado__
        text = RxHeader.Replace(text, "");             // ## Título
        text = RxWhitespace.Replace(text, " ");        // espaços/newlines extras
        return text.Trim();
    }
}
