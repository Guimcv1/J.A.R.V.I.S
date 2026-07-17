using System.Diagnostics;
using System.Text.RegularExpressions;
using Jarvis.Core;

namespace Jarvis.Infrastructure;

/// <summary>
/// [OBSOLETO] TTS usando espeak-ng — voz robótica, qualidade baixa.
///
/// Use <see cref="PiperTtsService"/> para voz neural de alta qualidade.
/// Esta classe é mantida apenas como fallback de emergência.
/// </summary>
[Obsolete("Use PiperTtsService para voz neural multiplataforma de alta qualidade.")]
public class LinuxTtsService : IAudioOutputService
{
    private Process? _activeProcess;
    private readonly object _lock = new();

    public async Task SpeakAsync(string text)
    {
        Stop(); // Interrupt any ongoing speech

        var clean = Sanitize(text);
        if (string.IsNullOrWhiteSpace(clean)) return;

        var psi = new ProcessStartInfo("espeak-ng")
        {
            // Pass ALL settings as args; text is written to stdin (avoids every escaping issue)
            Arguments            = "-v en-us+m7 -p 38 -s 145 --stdin",
            UseShellExecute      = false,
            RedirectStandardInput  = true,
            RedirectStandardError  = true,
            RedirectStandardOutput = false,
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
            // Write text to stdin and close it so espeak-ng knows it's done
            await proc.StandardInput.WriteLineAsync(clean);
            proc.StandardInput.Close();

            await proc.WaitForExitAsync();
        }
        catch (OperationCanceledException) { /* Stopped by user */ }
        finally
        {
            lock (_lock)
            {
                if (_activeProcess == proc)
                    _activeProcess = null;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            try { _activeProcess?.Kill(entireProcessTree: true); }
            catch { /* Already exited */ }
            _activeProcess = null;
        }
    }

    /// <summary>
    /// Strips markdown, code blocks, and other formatting before speaking.
    /// </summary>
    private static string Sanitize(string text)
    {
        // Remove code blocks entirely
        text = Regex.Replace(text, @"```[\s\S]*?```", " ");
        text = Regex.Replace(text, @"`[^`]+`", " ");

        // Strip HTML/XML tags (including <think>)
        text = Regex.Replace(text, @"<[^>]+>", " ");

        // Strip markdown bold/italic
        text = Regex.Replace(text, @"\*{1,3}([^*]+)\*{1,3}", "$1");
        text = Regex.Replace(text, @"_{1,2}([^_]+)_{1,2}", "$1");

        // Strip markdown headers
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline);

        // Collapse multiple whitespace/newlines
        text = Regex.Replace(text, @"\s{2,}", " ");

        return text.Trim();
    }
}
