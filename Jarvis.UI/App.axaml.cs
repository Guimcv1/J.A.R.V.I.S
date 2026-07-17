using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Jarvis.Infrastructure;
using Jarvis.UI.ViewModels;
using Jarvis.UI.Views;

namespace Jarvis.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Manual dependency composition — all services 100% local
            var llmService    = new OllamaLLMService(modelName: "qwen2.5:7b-instruct");
            var speechService = new SpeechTranscriptionService();
            var ttsService    = new PiperTtsService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(llmService, speechService, ttsService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}