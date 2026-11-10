using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyLittleSlideShow.Views;

public partial class MyMessageBox : Window
{
    private bool _result;

    public MyMessageBox()
    {
        InitializeComponent();
        ScreenFit.Attach(this);
    }

    public MyMessageBox(string title, string message) : this()
    {
        Title = title;
        Message_Label.Text = message;
    }

    /// <summary>
    /// Avalonia kennt keine blockierende ShowDialog-Ueberladung wie WPF.
    /// Aufrufer muessen daher await verwenden.
    /// </summary>
    public static async Task<bool> ShowDialogAsync(string title, string message, Window owner)
    {
        var box = new MyMessageBox(title, message);
        await box.ShowDialog(owner);
        return box._result;
    }

    private void Yes_Click(object? sender, RoutedEventArgs e)
    {
        _result = true;
        Close();
    }

    private void No_Click(object? sender, RoutedEventArgs e)
    {
        _result = false;
        Close();
    }
}
