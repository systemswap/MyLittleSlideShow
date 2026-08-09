using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyLittleSlideShow.Views;

/// <summary>
/// Ersetzt UserControl_InfoWindow. Als eigenes Fenster statt als eingeblendetes
/// UserControl, weil die alte Loesung das Einstellungsfenster ueberlagerte.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        HiddenOptions.ItemsSource = ReadEmbeddedLines("MyLittleSlideShow.Infos.HiddenOptions.txt");
        ChangeLogText_ListBox.ItemsSource = ReadEmbeddedLines("MyLittleSlideShow.Infos.Changelog.txt");
    }

    private static List<string> ReadEmbeddedLines(string resourceName)
    {
        var lines = new List<string>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream is null) return lines;

            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line) lines.Add(line);
        }
        catch
        {
            lines.Add("Datei konnte nicht gelesen werden.");
        }
        return lines;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
