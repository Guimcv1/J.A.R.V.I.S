namespace Jarvis.Core;

/// <summary>
/// Defines a local speech-to-text transcription service.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    /// Records audio from the default microphone for the given duration
    /// and transcribes it to text using a local STT engine.
    /// </summary>
    Task<string> ListenAndTranscribeAsync(int durationSeconds = 5);

    /// <summary>
    /// Transcribes speech from an existing WAV file.
    /// </summary>
    Task<string> TranscribeFileAsync(string wavFilePath);
}
