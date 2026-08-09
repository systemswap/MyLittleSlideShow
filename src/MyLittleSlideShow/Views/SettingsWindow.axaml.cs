using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;

namespace MyLittleSlideShow.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private AppSettings S => _settingsManager.Settings;

    private string _folderPath = string.Empty;

    /// <summary>Meldet dem Hauptfenster, ob es sich neu laden muss.</summary>
    public bool SettingsSaved { get; private set; }

    /// <summary>Nur fuer den XAML-Designer.</summary>
    public SettingsWindow() : this(App.SettingsManager) { }

    public SettingsWindow(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var platform = PlatformServices.Current;

        Autostart_CheckBox.IsChecked = platform.Autostart.IsEnabled();
        Autostart_CheckBox.IsEnabled = platform.Autostart.IsSupported;

        DisplayAlwaysOn_CheckBox.IsChecked = S.DisplayAlwaysOn;
        DisplayAlwaysOn_CheckBox.IsEnabled = platform.SleepInhibitor.IsSupported;

        Sharpen_CheckBox.IsChecked = S.SharpImages;
        SharpenValue_Box.Value = S.SharpenValue;
        QuietSharpen_CheckBox.IsChecked = S.QuietSharpen;
        SharpenThreshold_Box.Value = S.SharpenThreshold;
        StretchWhileZooming_CheckBox.IsChecked = S.StretchWhileZooming;

        AmbiFrame_CheckBox.IsChecked = S.WithAmbiFrame;
        AmbiFrameOpacity_Box.Value = S.OpacityOfAmbiFrame;

        SlideShow_CheckBox.IsChecked = S.ActivateSlideShow;
        Interval_Box.Value = S.ImageChangeIntervalSeconds;
        Random_CheckBox.IsChecked = S.RandomImages;

        EveryImageAsWallpaper_CheckBox.IsChecked = S.SetEveryImageAsWallpaper;
        WallpaperStyle_ComboBox.ItemsSource = Enum.GetValues<WallpaperStyle>();
        WallpaperStyle_ComboBox.SelectedItem = S.WallpaperStyle;

        if (!platform.Wallpaper.IsSupported)
        {
            EveryImageAsWallpaper_CheckBox.IsEnabled = false;
            WallpaperStyle_ComboBox.IsEnabled = false;
            WallpaperHint.Text = "Für diese Desktopumgebung ist kein Werkzeug zum Setzen des " +
                                 "Hintergrundbilds verfügbar (erwartet werden gsettings, " +
                                 "plasma-apply-wallpaperimage, xfconf-query oder feh).";
            WallpaperHint.IsVisible = true;
        }

        _folderPath = S.FolderPath;
        FolderPath_Label.Text = string.IsNullOrEmpty(_folderPath) ? "(kein Pfad gewählt)" : _folderPath;
        Recursive_CheckBox.IsChecked = S.ScanFolderRecursive;
    }

    private async void SelectFolder_Click(object? sender, RoutedEventArgs e)
    {
        // Ersetzt System.Windows.Forms.FolderBrowserDialog durch den
        // plattformunabhaengigen StorageProvider von Avalonia.
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ordner mit Bildern wählen",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null) return;

        var path = folder.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        _folderPath = path;
        FolderPath_Label.Text = _folderPath;
    }

    private void ResetPath_Click(object? sender, RoutedEventArgs e)
    {
        _folderPath = string.Empty;
        FolderPath_Label.Text = "(kein Pfad gewählt)";
        S.LastLoadedFile = string.Empty;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var autostart = PlatformServices.Current.Autostart;
        if (autostart.IsSupported)
        {
            var wanted = Autostart_CheckBox.IsChecked == true;
            if (wanted) autostart.Enable(); else autostart.Disable();
            S.Autostart = autostart.IsEnabled();
        }

        S.DisplayAlwaysOn = DisplayAlwaysOn_CheckBox.IsChecked == true;

        S.SharpImages = Sharpen_CheckBox.IsChecked == true;
        S.SharpenValue = (int)(SharpenValue_Box.Value ?? 25);
        S.QuietSharpen = QuietSharpen_CheckBox.IsChecked == true;
        S.SharpenThreshold = (int)(SharpenThreshold_Box.Value ?? 6);
        S.StretchWhileZooming = StretchWhileZooming_CheckBox.IsChecked == true;

        S.WithAmbiFrame = AmbiFrame_CheckBox.IsChecked == true;
        S.OpacityOfAmbiFrame = (int)(AmbiFrameOpacity_Box.Value ?? 90);

        S.ActivateSlideShow = SlideShow_CheckBox.IsChecked == true;
        S.ImageChangeIntervalSeconds = (int)(Interval_Box.Value ?? 120);
        S.RandomImages = Random_CheckBox.IsChecked == true;

        S.SetEveryImageAsWallpaper = EveryImageAsWallpaper_CheckBox.IsChecked == true;
        if (WallpaperStyle_ComboBox.SelectedItem is WallpaperStyle style)
            S.WallpaperStyle = style;

        S.FolderPath = _folderPath;
        S.ScanFolderRecursive = Recursive_CheckBox.IsChecked == true;

        _settingsManager.Save();
        SettingsSaved = true;
        Close();
    }

    private void Donate_Click(object? sender, RoutedEventArgs e)
        => PlatformServices.Current.Shell.OpenUrl(Donation.BuildPayPalUrl(Donation.Language.De));

    private void Info_Click(object? sender, RoutedEventArgs e)
        => new AboutWindow().ShowDialog(this);
}
