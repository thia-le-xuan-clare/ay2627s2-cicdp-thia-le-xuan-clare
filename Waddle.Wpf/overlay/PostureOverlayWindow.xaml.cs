using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Waddle.Wpf;

public partial class PostureOverlayWindow : Window
{
  [DllImport("user32.dll")]
  private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

  [DllImport("user32.dll")]
  private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

  private const int HOTKEY_ID = 9001;
  private const uint MOD_CONTROL = 0x0002;
  private const uint MOD_SHIFT = 0x0004;
  private const uint VK_E = 0x45;

  private HwndSource? _source;

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

  protected override void OnSourceInitialized(EventArgs e)
  {
    base.OnSourceInitialized(e);
    var handle = new WindowInteropHelper(this).Handle;
    _source = HwndSource.FromHwnd(handle);
    _source?.AddHook(HwndHook);
    RegisterHotKey(handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_E);
  }

  private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
  {
    const int WM_HOTKEY = 0x0312;
    if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
    {
      HotkeyExitRequested?.Invoke();
      handled = true;
    }
    return IntPtr.Zero;
  }

  protected override void OnClosed(EventArgs e)
  {
    _source?.RemoveHook(HwndHook);
    UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_ID);
    base.OnClosed(e);
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
