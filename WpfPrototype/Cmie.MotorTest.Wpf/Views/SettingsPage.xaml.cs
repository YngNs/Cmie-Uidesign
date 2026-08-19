using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cmie.MotorTest.Wpf.Views;

public partial class SettingsPage : UserControl
{
    private readonly Dictionary<string, FrameworkElement> _panels;
    private readonly Dictionary<string, Button> _buttons;

    public SettingsPage()
    {
        InitializeComponent();
        _panels = new() { ["ratio"] = RatioPanel, ["model"] = ModelPanel, ["comm"] = CommPanel, ["temp"] = TempPanel };
        _buttons = new() { ["ratio"] = RatioNav, ["model"] = ModelNav, ["comm"] = CommNav, ["temp"] = TempNav };
        SelectPanel("ratio");
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key }) SelectPanel(key);
    }

    private void SelectPanel(string key)
    {
        foreach (var (panelKey, panel) in _panels) panel.Visibility = panelKey == key ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (buttonKey, button) in _buttons)
        {
            button.Background = buttonKey == key ? (Brush)FindResource("AccentSoftBrush") : Brushes.Transparent;
            button.Foreground = (Brush)FindResource(buttonKey == key ? "AccentBrush" : "TextBrush");
            button.BorderBrush = buttonKey == key ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Shell?.ShowToast($"{(sender as FrameworkElement)?.Tag}已保存（演示）");
    private void Reset_Click(object sender, RoutedEventArgs e) => Shell?.ShowToast("已恢复默认变比（演示）");
    private void TestConnection_Click(object sender, RoutedEventArgs e) => Shell?.ShowToast("COM3 连接成功 · Modbus RTU");
}
