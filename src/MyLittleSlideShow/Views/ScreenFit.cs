using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MyLittleSlideShow.Views;

/// <summary>
/// Haelt Dialogfenster vollstaendig auf dem Bildschirm, auf dem sie aufgehen.
///
/// Auf einem Laptop mit 1920 x 1080 und 125 % Skalierung bleiben ueber der
/// Taskleiste nur 816 geraeteunabhaengige Pixel Hoehe - das Einstellungsfenster
/// ist mit allen Abschnitten hoeher. Ohne Begrenzung lagen "Speichern" und
/// "Abbrechen" unterhalb des Bildschirmrands.
/// </summary>
internal static class ScreenFit
{
    private const int Margin = 8;   // physische Pixel Abstand zum Rand

    /// <summary>Beim Oeffnen Groesse begrenzen und Position in den Arbeitsbereich holen.</summary>
    public static void Attach(Window window)
    {
        window.Opened += (_, _) => Fit(window);
    }

    private static void Fit(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null) return;

        var work = screen.WorkingArea;
        var scaling = screen.Scaling;

        // Groesse begrenzen: SizeToContent-Fenster werden dadurch kleiner und
        // ihr ScrollViewer uebernimmt, feste Hoehen werden gekappt.
        // Width/Height/Max* gelten fuer den Inhalt - Titelleiste und Rahmen
        // (Unterschied FrameSize zu ClientSize) kommen noch dazu.
        var frame = window.FrameSize ?? window.ClientSize;
        var decorationWidth = Math.Max(0, frame.Width - window.ClientSize.Width);
        var decorationHeight = Math.Max(0, frame.Height - window.ClientSize.Height);
        // Dialoge sollen deutlich kleiner als der Bildschirm bleiben: hoechstens
        // 90 % der Breite und 85 % der Hoehe des Arbeitsbereichs.
        var maxWidth = Math.Min(work.Width * 0.90, work.Width - 2 * Margin) / scaling - decorationWidth;
        var maxHeight = Math.Min(work.Height * 0.85, work.Height - 2 * Margin) / scaling - decorationHeight;
        window.MaxWidth = Math.Min(window.MaxWidth, maxWidth);
        window.MaxHeight = Math.Min(window.MaxHeight, maxHeight);
        if (!double.IsNaN(window.Width) && window.Width > maxWidth) window.Width = maxWidth;
        if (!double.IsNaN(window.Height) && window.Height > maxHeight) window.Height = maxHeight;

        // Erst nach dem neuen Layout steht die endgueltige Groesse fest.
        Dispatcher.UIThread.Post(() =>
        {
            var size = PixelSize.FromSize(window.FrameSize ?? window.ClientSize, scaling);
            var x = Math.Clamp(window.Position.X, work.X + Margin, Math.Max(work.X + Margin, work.Right - size.Width - Margin));
            var y = Math.Clamp(window.Position.Y, work.Y + Margin, Math.Max(work.Y + Margin, work.Bottom - size.Height - Margin));
            window.Position = new PixelPoint(x, y);
        }, DispatcherPriority.Background);
    }
}
