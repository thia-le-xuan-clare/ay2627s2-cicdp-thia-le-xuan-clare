using System.Windows;
using System.Windows.Input;

namespace Waddle.Wpf;

public partial class PostureOverlayWindow : Window
{
    public event Action? HotkeyExitRequested;

    public PostureOverlayWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) => Activate();
    }

    public static bool IsHotkeyExit(Key key, ModifierKeys modifiers) =>
        key == Key.E && modifiers == (ModifierKeys.Control | ModifierKeys.Shift);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsHotkeyExit(e.Key, Keyboard.Modifiers))
        {
            HotkeyExitRequested?.Invoke();
        }
    }
}
