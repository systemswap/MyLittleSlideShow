using Avalonia;

namespace MyLittleSlideShow;

internal static class Program
{
    // Avalonia startet ueber einen klassischen Main-Einstiegspunkt statt ueber
    // App.xaml/StartupUri wie bei WPF.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
