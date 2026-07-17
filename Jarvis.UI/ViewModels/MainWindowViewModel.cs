using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jarvis.Core;
using Jarvis.Infrastructure;

namespace Jarvis.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // ── Services ──────────────────────────────────────────────────────────────
    private readonly ILLMService? _llmService;
    private readonly ISpeechService? _speechService;
    private readonly IAudioOutputService? _ttsService;
    private readonly SynchronizationContext? _uiContext;

    // ── Cancellation ──────────────────────────────────────────────────────────
    private CancellationTokenSource _cts = new();

    // ── State Properties ──────────────────────────────────────────────────────
    [ObservableProperty] private string _userInput = string.Empty;
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isSpeaking;
    [ObservableProperty] private bool _isVoiceLoopActive;
    [ObservableProperty] private string _coreStatus = "IDLE";
    [ObservableProperty] private string _voiceButtonLabel = "START VOICE LOOP";

    // ── Language Toggle ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _isPortuguese = true;
    [ObservableProperty] private string _languageLabel = "PT-BR";

    // ── Animation Properties (driven by background loop) ──────────────────────
    [ObservableProperty] private double _circleScale = 1.0;
    [ObservableProperty] private double _glowOpacity = 0.45;

    // ── Tab Navigation ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isMainTabVisible = true;
    [ObservableProperty] private bool _isTerminalTabVisible;

    // ── Collections ───────────────────────────────────────────────────────────
    public ObservableCollection<string> ConversationLog { get; } = new();
    public ObservableCollection<string> SystemLog { get; } = new();

    // ── Design-Time Constructor ───────────────────────────────────────────────
    public MainWindowViewModel()
    {
        ConversationLog.Add("JARVIS: System online. All modules nominal.");
        SystemLog.Add("[SYSTEM] Design-time preview active.");
    }

    // ── Runtime Constructor ───────────────────────────────────────────────────
    public MainWindowViewModel(
        ILLMService llmService,
        ISpeechService? speechService = null,
        IAudioOutputService? ttsService = null)
    {
        _llmService = llmService;
        _speechService = speechService;
        _ttsService = ttsService;
        _uiContext = SynchronizationContext.Current;

        Log("[SYSTEM] JARVIS initialized.");
        Log("[SYSTEM] LLM: Ollama / qwen2.5:7b-instruct");
        Log("[SYSTEM] STT: Whisper.net ready");
        Log($"[SYSTEM] TTS: {(ttsService != null ? "Piper TTS ready" : "offline")}");
        PostToUI(() => ConversationLog.Add("JARVIS: Online. How can I assist you?"));

        // Start the visual animation loop immediately
        _ = Task.Run(() => RunAnimationLoopAsync(_cts.Token));
    }

    // =========================================================================
    // ANIMATION LOOP — drives CircleScale and GlowOpacity at ~30fps
    // =========================================================================
    private async Task RunAnimationLoopAsync(CancellationToken ct)
    {
        double phase = 0;
        while (!ct.IsCancellationRequested)
        {
            double scale, glow;

            if (IsListening)
            {
                // Rapid, energetic pulse — user is talking
                scale = 1.0 + 0.22 * Math.Abs(Math.Sin(phase * 5.0));
                glow  = 0.55 + 0.45 * Math.Abs(Math.Sin(phase * 5.0));
                phase += 0.13;
            }
            else if (IsSpeaking)
            {
                // Rhythmic speech waveform — JARVIS is responding
                scale = 1.0 + 0.18 * Math.Abs(Math.Sin(phase * 3.0));
                glow  = 0.5  + 0.5  * Math.Abs(Math.Sin(phase * 3.0));
                phase += 0.09;
            }
            else
            {
                // Gentle idle breathe
                scale = 1.0 + 0.035 * Math.Sin(phase * 0.55);
                glow  = 0.42 + 0.12  * Math.Sin(phase * 0.55);
                phase += 0.025;
            }

            // Reset phase to avoid float drift
            if (phase > Math.PI * 400) phase = 0;

            PostToUI(() =>
            {
                CircleScale  = scale;
                GlowOpacity  = glow;
            });

            try { await Task.Delay(33, ct); }
            catch (OperationCanceledException) { break; }
        }
        PostToUI(() => { CircleScale = 1.0; GlowOpacity = 0.45; });
    }

    // =========================================================================
    // TAB NAVIGATION
    // =========================================================================
    [RelayCommand]
    private void SelectCoreTab()
    {
        IsMainTabVisible     = true;
        IsTerminalTabVisible = false;
    }

    [RelayCommand]
    private void SelectTerminalTab()
    {
        IsMainTabVisible     = false;
        IsTerminalTabVisible = true;
    }

    // =========================================================================
    // LANGUAGE TOGGLE
    // =========================================================================
    [RelayCommand]
    private void ToggleLanguage()
    {
        IsPortuguese = !IsPortuguese;
        LanguageLabel = IsPortuguese ? "PT-BR" : "EN-GB";
        
        if (_ttsService is PiperTtsService piperService)
        {
            piperService.SetLanguage(IsPortuguese ? "pt_BR-faber-medium.onnx" : "en_GB-alan-medium.onnx");
        }
        
        Log($"[SYSTEM] Language changed to {LanguageLabel}");
    }

    // =========================================================================
    // SEND MESSAGE (text input)
    // =========================================================================
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;
        await ProcessInputAsync(UserInput);
        UserInput = string.Empty;
    }

    // =========================================================================
    // VOICE LOOP TOGGLE
    // =========================================================================
    [RelayCommand]
    private async Task ToggleVoiceLoopAsync()
    {
        if (_speechService == null)
        {
            Log("[STT] Speech service not available.");
            return;
        }

        if (IsVoiceLoopActive)
        {
            // Stop the loop
            IsVoiceLoopActive  = false;
            VoiceButtonLabel   = "START VOICE LOOP";
            CoreStatus         = "IDLE";
            _ttsService?.Stop();
            Log("[VOICE] Loop stopped.");
        }
        else
        {
            IsVoiceLoopActive = true;
            VoiceButtonLabel  = "STOP VOICE LOOP";
            Log("[VOICE] Autonomous voice loop started.");
            _ = Task.Run(() => RunVoiceLoopAsync(_cts.Token));
        }
        await Task.CompletedTask;
    }

    // =========================================================================
    // VOICE LOOP — continuously records, transcribes, and responds
    // =========================================================================
    private async Task RunVoiceLoopAsync(CancellationToken ct)
    {
        while (IsVoiceLoopActive && !ct.IsCancellationRequested)
        {
            try
            {
                PostToUI(() => { IsListening = true; CoreStatus = "LISTENING"; });
                Log("[STT] Recording 5s...");

                var transcribed = await _speechService!.ListenAndTranscribeAsync(5);

                PostToUI(() => { IsListening = false; });

                if (!string.IsNullOrWhiteSpace(transcribed))
                {
                    Log($"[STT] Heard: \"{transcribed}\"");
                    PostToUI(() => ConversationLog.Add($"USER: {transcribed}"));
                    await ProcessInputAsync(transcribed, addToLog: false);
                }
                else
                {
                    Log("[STT] No speech detected.");
                    PostToUI(() => CoreStatus = "IDLE");
                    await Task.Delay(500, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log($"[VOICE ERROR] {ex.Message}");
                PostToUI(() => { IsListening = false; CoreStatus = "IDLE"; });
                await Task.Delay(1000, ct);
            }
        }
        PostToUI(() => { IsVoiceLoopActive = false; IsListening = false; CoreStatus = "IDLE"; VoiceButtonLabel = "START VOICE LOOP"; });
    }

    // =========================================================================
    // CORE PIPELINE — LLM + TTS
    // =========================================================================
    private async Task ProcessInputAsync(string input, bool addToLog = true)
    {
        if (addToLog)
            PostToUI(() => ConversationLog.Add($"USER: {input}"));

        Log("[LLM] Sending prompt to qwen2.5:7b-instruct...");
        PostToUI(() => CoreStatus = "THINKING");

        if (_llmService == null)
        {
            PostToUI(() => ConversationLog.Add("JARVIS: [LLM not configured]"));
            return;
        }

        try
        {
            var result = await _llmService.PromptAsync(input);

            // Route thinking to system log
            if (result.HasThinking)
            {
                Log("[THINK] ---");
                foreach (var line in result.Thinking!.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    Log($"[THINK] {line.Trim()}");
                Log("[THINK] ---");
            }

            PostToUI(() => ConversationLog.Add($"JARVIS: {result.Answer}"));
            Log("[LLM] Response received.");

            // Speak the clean answer
            if (_ttsService != null && !string.IsNullOrWhiteSpace(result.Answer))
            {
                PostToUI(() => { IsSpeaking = true; CoreStatus = "SPEAKING"; });
                await _ttsService.SpeakAsync(result.Answer);
                PostToUI(() => { IsSpeaking = false; CoreStatus = IsVoiceLoopActive ? "LISTENING" : "IDLE"; });
            }
            else
            {
                PostToUI(() => CoreStatus = IsVoiceLoopActive ? "LISTENING" : "IDLE");
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] {ex.Message}");
            PostToUI(() => { ConversationLog.Add("JARVIS: [Error communicating with LLM]"); CoreStatus = "IDLE"; IsSpeaking = false; });
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================
    private void Log(string message) =>
        PostToUI(() => SystemLog.Add(message));

    private void PostToUI(Action action)
    {
        if (_uiContext != null)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }
}
