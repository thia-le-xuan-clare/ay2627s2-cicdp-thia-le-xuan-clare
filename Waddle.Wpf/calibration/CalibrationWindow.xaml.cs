using System.Windows;
using System.Windows.Input;

namespace Waddle.Wpf;

/// <summary>
/// Full-screen frosted overlay with the calibration card in the middle.
/// It covers the work area (not the taskbar) and gives the camera back when it closes.
/// </summary>
public partial class CalibrationWindow : Window
{
    private readonly CalibrationViewModel _vm;

    public CalibrationWindow(CalibrationViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;

        vm.CloseRequested += Close;
        Loaded += async (_, _) => await vm.StartAsync();
        Closed += (_, _) => vm.Dispose();       // releases the "calibration" camera holder
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
