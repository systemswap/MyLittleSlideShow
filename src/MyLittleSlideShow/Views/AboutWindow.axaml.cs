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
        ScreenFit.Attach(this);
        HiddenOptions.ItemsSource = ReadEmbeddedLines("MyLittleSlideShow.Infos.HiddenOptions.txt");
        ChangeLogText_ListBox.ItemsSource = ReadEmbeddedLines("MyLittleSlideShow.Infos.Changelog.txt");
        Licenses_ListBox.ItemsSource = LicenseLines();
    }

    /// <summary>
    /// Eigene Lizenz, Uebersicht der Fremdbestandteile und die vollstaendigen
    /// Lizenztexte, die beigelegt werden muessen.
    /// </summary>
    private static List<string> LicenseLines()
    {
        const string prefix = "MyLittleSlideShow.Licenses.";
        var lines = new List<string>();

        lines.AddRange(ReadEmbeddedLines(prefix + "THIRD-PARTY-NOTICES.md"));

        var texts = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".txt", StringComparison.Ordinal))
                            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var name in new[] { prefix + "LICENSE" }.Concat(texts))
        {
            lines.Add(string.Empty);
            lines.Add("──────── " + (name == prefix + "LICENSE" ? "MyLittleSlideShow (LICENSE)" : name[prefix.Length..]) + " ────────");
            lines.AddRange(ReadEmbeddedLines(name));
        }

        return lines;
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
