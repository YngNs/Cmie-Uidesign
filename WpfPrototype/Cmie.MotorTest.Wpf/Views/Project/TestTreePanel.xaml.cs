using System.Windows;
using System.Windows.Controls;
using Cmie.MotorTest.Wpf.Demo;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class TestTreePanel : UserControl
{
    private readonly Dictionary<string, Button> _buttons;

    public TestTreePanel()
    {
        InitializeComponent();
        _buttons = new Dictionary<string, Button>
        {
            ["resistance"] = ResistanceButton,
            ["noload"] = NoLoadButton,
            ["load"] = LoadButton,
            ["method-a"] = MethodAButton,
            ["method-b"] = MethodBButton,
            ["temp-rated"] = TempRatedButton,
            ["temp-low"] = TempLowButton,
            ["locked"] = LockedButton,
            ["overspeed"] = OverspeedButton
        };
    }

    public event Action<TestItem>? TestSelected;
    public event Action? CollapseRequested;

    public void SetWindowState(IEnumerable<string> openKeys, string? activeKey)
    {
        var open = openKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var (key, button) in _buttons)
        {
            var item = DemoProjectData.Tests.First(test => test.Key == key);
            button.Content = $"{(open.Contains(key) ? "●" : "○")}  {item.Title}";
            button.SetResourceReference(
                Control.ForegroundProperty,
                key == activeKey ? "AccentBrush" : "TextBrush");

            if (key == activeKey)
            {
                button.SetResourceReference(Control.BackgroundProperty, "AccentSoftBrush");
            }
            else
            {
                button.Background = System.Windows.Media.Brushes.Transparent;
            }
        }
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            TestSelected?.Invoke(DemoProjectData.Tests.First(item => item.Key == key));
        }
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke();
}
