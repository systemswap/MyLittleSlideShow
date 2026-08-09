using Avalonia.Controls;
using Avalonia.Interactivity;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;

namespace MyLittleSlideShow.Views;

public partial class InfoWindow : Window
{
    private readonly string _fileName = string.Empty;

    /// <summary>Nur fuer den XAML-Designer.</summary>
    public InfoWindow() : this(string.Empty) { }

    public InfoWindow(string filePath)
    {
        _fileName = filePath;
        InitializeComponent();
        if (File.Exists(_fileName)) LoadFileInfos();
    }

    private void LoadFileInfos()
    {
        var items = new List<string>();

        var fi = new FileInfo(_fileName);
        items.Add($"Dateiname: {fi.Name}");
        items.Add($"Größe: {fi.Length / 1024d:N1} KB");
        items.Add($"Erstellt am: {fi.CreationTime}");
        items.Add($"Geändert am: {fi.LastWriteTime}");
        items.Add(string.Empty);
        items.Add("Verzeichnis:");
        items.Add(fi.DirectoryName ?? string.Empty);
        items.Add(string.Empty);
        items.Add("Exif-Daten");

        // Die frueheren Dateien ExifReader.cs und ExifTags.cs (zusammen rund
        // 1300 Zeilen) sind entfallen - ImageSharp liefert die Metadaten direkt.
        items.AddRange(ImageProcessing.ReadExif(_fileName));

        InfoLB.ItemsSource = items;
    }

    private void OpenDirectory_Click(object? sender, RoutedEventArgs e)
        => PlatformServices.Current.Shell.RevealInFileManager(_fileName);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
