using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;
using MyLittleSlideShow.Views;

namespace MyLittleSlideShow;

public partial class App : Application
{
    public static SettingsManager SettingsManager { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        SettingsManager = new SettingsManager();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Das Fenster wird beim Minimieren in den Tray gelegt, deshalb darf
            // das Schliessen des letzten Fensters die App nicht beenden.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new Views.MainWindow(SettingsManager);

            desktop.Exit += (_, _) =>
            {
                PlatformServices.Current.SleepInhibitor.SetKeepAwake(false);
                PlatformServices.Current.SleepInhibitor.Dispose();
                SettingsManager.Save();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Views.MainWindow? Main =>
        (Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow as Views.MainWindow;

    private void TrayIcon_Clicked(object? sender, EventArgs e) => ShowMainWindow();

    private void Tray_Show_Click(object? sender, EventArgs e) => ShowMainWindow();

    private void Tray_Minimize_Click(object? sender, EventArgs e)
    {
        if (Main is { } window) window.WindowState = WindowState.Minimized;
    }

    private void Tray_Restart_Click(object? sender, EventArgs e)
    {
        // Bei einer AppImage die .AppImage-Datei: das Innere unter /tmp/.mount_... wird
        // ausgehaengt, sobald dieses Programm endet - der neue Prozess verloere seine Dateien.
        var path = AppLocations.LaunchPath;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
            }
            catch
            {
                // Wenn der Neustart scheitert, wird wenigstens nicht beendet.
                return;
            }
        }
        Shutdown();
    }

    private void Tray_Exit_Click(object? sender, EventArgs e) => Shutdown();

    private static void ShowMainWindow()
    {
        if (Main is not { } window) return;
        window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();
    }

    public static void Shutdown()
    {
        Main?.PersistState();
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
