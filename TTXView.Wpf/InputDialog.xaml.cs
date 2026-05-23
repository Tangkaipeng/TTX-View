using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TTXView.Wpf;

public partial class InputDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        DialogTitleText.Text = title;
        PromptText.Text = prompt;
        ValueBox.Text = defaultValue;
        ValueBox.SelectAll();
        Loaded += (_, _) => ValueBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInside<Button>(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during a rapid click.
        }
    }

    private static bool IsInside<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T)
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
