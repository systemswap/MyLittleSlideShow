using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MyLittleSlideShow.Localization;

namespace MyLittleSlideShow.Views;

/// <summary>
/// Kleine Flagge vor dem Namen einer Sprache. Im Code gezeichnet, weil Windows
/// Flaggen-Emojis nur als zwei Buchstaben zeigt.
/// </summary>
public sealed class FlagView : Control
{
    public static readonly StyledProperty<Language?> LanguageProperty =
        AvaloniaProperty.Register<FlagView, Language?>(nameof(Language));

    private Bitmap? _image;
    private string? _imagePath;

    static FlagView()
    {
        AffectsRender<FlagView>(LanguageProperty);
    }

    public FlagView()
    {
        Width = 22;
        Height = 15;
    }

    public Language? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using (context.PushClip(new RoundedRect(rect, 2)))
        {
            var flag = Language?.Flag ?? Flag.None;
            switch (flag.Kind)
            {
                case FlagKind.Stripes when flag.Colors is { Count: > 0 } colors:
                    DrawStripes(context, rect, colors, flag.Vertical);
                    break;
                case FlagKind.UnionJack:
                    DrawUnionJack(context, rect);
                    break;
                case FlagKind.Image when LoadImage(flag.ImagePath) is { } bitmap:
                    context.DrawImage(bitmap, rect);
                    break;
                default:
                    DrawCode(context, rect, Language?.Code ?? "?");
                    break;
            }
        }

        // Feiner Rand, damit weisse Streifen auf hellem Grund sichtbar bleiben.
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1),
            rect.Deflate(0.5), 2, 2);
    }

    private static void DrawStripes(DrawingContext context, Rect rect, IReadOnlyList<string> colors, bool vertical)
    {
        var n = colors.Count;
        for (var i = 0; i < n; i++)
        {
            IBrush brush = Color.TryParse(colors[i], out var c) ? new SolidColorBrush(c) : Brushes.Gray;
            var part = vertical
                ? new Rect(rect.X + rect.Width * i / n, rect.Y, rect.Width / n + 0.5, rect.Height)
                : new Rect(rect.X, rect.Y + rect.Height * i / n, rect.Width, rect.Height / n + 0.5);
            context.FillRectangle(brush, part);
        }
    }

    /// <summary>Union Jack, vereinfacht fuer etwa 22 x 15 Pixel.</summary>
    private static void DrawUnionJack(DrawingContext context, Rect r)
    {
        var blue = new SolidColorBrush(Color.Parse("#012169"));
        var red = new SolidColorBrush(Color.Parse("#C8102E"));
        context.FillRectangle(blue, r);

        var white = new Pen(Brushes.White, r.Height * 0.2);
        var redThin = new Pen(red, r.Height * 0.08);
        context.DrawLine(white, r.TopLeft, r.BottomRight);
        context.DrawLine(white, r.BottomLeft, r.TopRight);
        context.DrawLine(redThin, r.TopLeft, r.BottomRight);
        context.DrawLine(redThin, r.BottomLeft, r.TopRight);

        var cx = r.Center.X;
        var cy = r.Center.Y;
        var w = r.Height * 0.33;
        context.FillRectangle(Brushes.White, new Rect(cx - w / 2, r.Y, w, r.Height));
        context.FillRectangle(Brushes.White, new Rect(r.X, cy - w / 2, r.Width, w));
        var rw = r.Height * 0.2;
        context.FillRectangle(red, new Rect(cx - rw / 2, r.Y, rw, r.Height));
        context.FillRectangle(red, new Rect(r.X, cy - rw / 2, r.Width, rw));
    }

    private static void DrawCode(DrawingContext context, Rect rect, string code)
    {
        context.FillRectangle(new SolidColorBrush(Color.Parse("#9AA1AC")), rect);
        var text = new FormattedText(code.ToUpperInvariant(), System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 8, Brushes.White);
        context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
    }

    private Bitmap? LoadImage(string? path)
    {
        if (path == _imagePath) return _image;
        _image?.Dispose();
        _image = null;
        _imagePath = path;
        try { if (path is not null) _image = new Bitmap(path); }
        catch (Exception) { /* beschaedigte Bilddatei: Sprachcode zeigen */ }
        return _image;
    }
}
