using System.IO;
using Jarvis.Core;
using Whisper.net;
using Whisper.net.Ggml;

namespace Jarvis.Infrastructure;

/// <summary>
/// Local speech-to-text transcription using Whisper.net (100% offline).
/// Downloads the Whisper model on first run if not already cached.
/// </summary>
public class SpeechTranscriptionService : ISpeechService, IAsyncDisposable
{
    private readonly string _modelPath;
    private WhisperFactory? _factory;
    private bool _initialized;

    public SpeechTranscriptionService(string? modelDirectory = null)
    {
        var dir = modelDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jarvis", "models");

        Directory.CreateDirectory(dir);
        _modelPath = Path.Combine(dir, "ggml-base.bin");
    }

    /// <summary>
    /// Ensures the Whisper model is downloaded and the factory is ready.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        if (!File.Exists(_modelPath))
        {
            // Download the tiny/base model on first use (~150MB)
            await using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(GgmlType.Base);
            await using var fileStream = File.Create(_modelPath);
            await modelStream.CopyToAsync(fileStream);
        }

        _factory = WhisperFactory.FromPath(_modelPath);
        _initialized = true;
    }

    /// <inheritdoc />
    public async Task<string> ListenAndTranscribeAsync(int durationSeconds = 5)
    {
        var wavPath = await LinuxAudioCapture.RecordAsync(durationSeconds);
        try
        {
            return await TranscribeFileAsync(wavPath);
        }
        finally
        {
            // Clean up temporary capture file
            if (File.Exists(wavPath))
                File.Delete(wavPath);
        }
    }

    /// <inheritdoc />
    public async Task<string> TranscribeFileAsync(string wavFilePath)
    {
        await EnsureInitializedAsync();

        if (_factory == null)
            throw new InvalidOperationException("Whisper factory failed to initialize.");

        await using var processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        await using var fileStream = File.OpenRead(wavFilePath);

        var segments = new System.Text.StringBuilder();
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            segments.Append(segment.Text);
        }

        return segments.ToString().Trim();
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        await ValueTask.CompletedTask;
    }
}
