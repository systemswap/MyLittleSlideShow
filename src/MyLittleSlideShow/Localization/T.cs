using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace MyLittleSlideShow.Localization;

/// <summary>
/// Uebersetzter Text in XAML: <c>Text="{l:T 'Einstellungen'}"</c>. Der deutsche
/// Text ist der Schluessel (<see cref="Tr"/>). An <see cref="Tr.Version"/> gebunden,
/// der Text wechselt also sofort mit der Sprache.
/// </summary>
/// <remarks>
/// Texte mit geschweiften Klammern als Element schreiben:
/// <c>&lt;ToolTip.Tip&gt;&lt;l:T Text="… {0} …" /&gt;&lt;/ToolTip.Tip&gt;</c>.
/// </remarks>
public sealed class T : MarkupExtension
{
    public T() { }

    public T(string text) => Text = text;

    public string Text { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        CompiledBinding.Create<Tr, int>(tr => tr.Version, source: Tr.Instance, mode: BindingMode.OneWay,
            converter: new FixedText(Text));

    private sealed class FixedText(string german) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Tr.T(german);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
