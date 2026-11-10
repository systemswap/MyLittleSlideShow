using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MyLittleSlideShow.Platform;
using MyLittleSlideShow.Services;

namespace MyLittleSlideShow.Views;

/// <summary>
/// Zeigt die Metadaten eines Bilds wie im ImageConverter: eine Zusammenfassung
/// in Abschnitten (Datei, Kamera, Aufnahme, Ort, Urheber, Farbprofil) und auf
/// Wunsch die vollstaendige Liste aller Eintraege.
/// </summary>
public partial class InfoWindow : Window
{
    private readonly string _fileName = string.Empty;

    /// <summary>Nur fuer den XAML-Designer.</summary>
    public InfoWindow() : this(string.Empty) { }

    public InfoWindow(string filePath)
    {
        _fileName = filePath;
        InitializeComponent();
        ScreenFit.Attach(this);
        FileNameText.Text = Path.GetFileName(filePath);
        if (File.Exists(_fileName)) Show(ImageMetadataReader.Read(_fileName));
    }

    private void Show(ImageMetadata metadata)
    {
        SectionsPanel.Children.Clear();

        if (metadata.Problem is { } problem)
        {
            var warning = new TextBlock { Text = "Nicht alles war lesbar: " + problem, Classes = { "hint" } };
            warning.Bind(TextBlock.ForegroundProperty, warning.GetResourceObservable("Fine.Warning"));
            SectionsPanel.Children.Add(warning);
        }

        foreach (var group in metadata.Groups)
            SectionsPanel.Children.Add(Section(group.Title.ToUpperInvariant(), FieldsCard(group.Fields)));

        if (metadata.Tags.Count > 0)
            SectionsPanel.Children.Add(AllEntries(metadata));
    }

    // ------------------------------------------------------------ Bausteine

    private static StackPanel Section(string title, Control content) => new()
    {
        Children =
        {
            new TextBlock { Text = title, Classes = { "section" } },
            content
        }
    };

    private Border FieldsCard(IReadOnlyList<MetadataField> fields)
    {
        var rows = new StackPanel();
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) rows.Children.Add(new Border { Classes = { "divider" } });

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
            row.Children.Add(new TextBlock { Text = fields[i].Label, Classes = { "label" } });

            var value = new SelectableTextBlock { Text = fields[i].Value, Classes = { "value" } };
            Grid.SetColumn(value, 1);
            row.Children.Add(value);

            rows.Children.Add(row);
        }

        return new Border { Classes = { "card" }, Child = rows };
    }

    /// <summary>
    /// Vollstaendige Liste, erst auf Klick aufgebaut - Dateien mit grossen
    /// Herstellerdaten bringen leicht einige hundert Eintraege mit.
    /// </summary>
    private StackPanel AllEntries(ImageMetadata metadata)
    {
        var count = metadata.Tags.Count + (metadata.Truncated ? "+" : string.Empty);
        var toggle = new Button
        {
            Content = $"Alle Einträge anzeigen ({count})",
            Classes = { "subtle" },
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(-8, 0, 0, 0)
        };

        var holder = new StackPanel { Spacing = 6 };
        holder.Children.Add(toggle);

        Border? card = null;
        toggle.Click += (_, _) =>
        {
            if (card is null)
            {
                var list = new StackPanel();
                string? lastDirectory = null;
                foreach (var tag in metadata.Tags)
                {
                    if (tag.Directory != lastDirectory)
                    {
                        list.Children.Add(new TextBlock
                        {
                            Text = tag.Directory,
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            Margin = new Thickness(0, lastDirectory is null ? 4 : 12, 0, 2)
                        });
                        lastDirectory = tag.Directory;
                    }
                    list.Children.Add(new SelectableTextBlock { Text = $"{tag.Name}: {tag.Value}", Classes = { "raw" } });
                }
                if (metadata.Truncated)
                    list.Children.Add(new TextBlock
                    {
                        Text = $"… weitere Einträge ausgelassen (mehr als {ImageMetadataReader.MaxTags}).",
                        Classes = { "hint" }
                    });

                card = new Border { Classes = { "card" }, Padding = new Thickness(14, 8), Child = list };
                holder.Children.Add(card);
            }
            else
            {
                card.IsVisible = !card.IsVisible;
            }

            toggle.Content = card.IsVisible ? "Alle Einträge ausblenden" : $"Alle Einträge anzeigen ({count})";
        };

        return holder;
    }

    private void OpenDirectory_Click(object? sender, RoutedEventArgs e)
        => PlatformServices.Current.Shell.RevealInFileManager(_fileName);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
