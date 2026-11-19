using System.Windows;
using System.Windows.Input;

namespace Waddle.Wpf;

/// <summary>First-run overlay asking whether Waddle should start when the user logs in.</summary>
public partial class StartupPromptWindow : Window
{
    public StartupPromptWindow()
    {
        InitializeComponent();

        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;

        Loaded += (_, _) => OverlayState.Enter();
        Closed += (_, _) =>
        {
            OverlayState.Exit();
            StartupManager.MarkAsked();     // however it closed, don't ask again
        };
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        StartupManager.Set(true);
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();       // same as "No thanks"
    }
}
