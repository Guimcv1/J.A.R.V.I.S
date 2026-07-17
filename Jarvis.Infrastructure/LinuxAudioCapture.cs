using System.Diagnostics;
using System.IO;

namespace Jarvis.Infrastructure;

/// <summary>
/// Captures audio on Linux using 'parec' (PulseAudio/PipeWire compatible).
/// parec is part of libpulse and works natively with PipeWire's PulseAudio layer.
///
/// Produces a 16kHz, 16-bit, mono WAV file — the optimal format for Whisper.net.
/// </summary>
public static class LinuxAudioCapture
{
    /// <summary>
    /// Records audio from the default microphone for the given duration.
    /// Returns the path to the temporary WAV file created.
    /// </summary>
    public static async Task<string> RecordAsync(int durationSeconds = 5)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"jarvis_capture_{Guid.NewGuid()}.wav");

        // parec: raw s16le → piped through timeout for fixed duration
        // --file-format=wav writes a proper WAV header automatically
        // -d defaults to the system default microphone source
        var parec = new ProcessStartInfo("parec",
            $"--channels=1 --rate=16000 --format=s16le --file-format=wav \"{outputPath}\"")
        {
            RedirectStandardOutput = false,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };

        using var recordProcess = Process.Start(parec)
            ?? throw new InvalidOperationException(
                "Failed to start parec. Ensure libpulse is installed (pacman -S libpulse).");

        // Wait for the requested duration then kill the recorder (parec records indefinitely)
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

        try
        {
            if (!recordProcess.HasExited)
                recordProcess.Kill();
        }
        catch { /* Already exited */ }

        await recordProcess.WaitForExitAsync();

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 100)
            throw new InvalidOperationException(
                $"parec produced no audio output. Check your microphone source with: pactl list sources short");

        return outputPath;
    }
}
