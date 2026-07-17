namespace Jarvis.Core;

/// <summary>
/// Defines a local text-to-speech output service.
/// All implementations must run 100% offline.
/// </summary>
public interface IAudioOutputService
{
    /// <summary>Speaks the provided text aloud using the local TTS engine.</summary>
    Task SpeakAsync(string text);

    /// <summary>Immediately stops any ongoing speech synthesis.</summary>
    void Stop();
}
