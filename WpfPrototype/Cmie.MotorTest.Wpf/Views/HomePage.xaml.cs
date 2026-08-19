using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views;

public partial class HomePage : UserControl
{
    private readonly DispatcherTimer _metricTimer;
    private readonly Random _random = new();

    public HomePage()
    {
        InitializeComponent();

        _metricTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1600)
        };
        _metricTimer.Tick += (_, _) => RefreshDemoMetrics();

        Loaded += (_, _) =>
        {
            RefreshHeroCopy();
            RefreshDemoMetrics();
            _metricTimer.Start();
        };
        Unloaded += (_, _) => _metricTimer.Stop();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    public void RefreshHeroCopy()
    {
        var user = Shell?.CurrentUserDisplay ?? "超级用户";
        var hour = DateTime.Now.Hour;
        var greet = hour < 12 ? "早上好" : hour < 18 ? "下午好" : "晚上好";
        HeroGreetingText.Text = $"{greet}，{user}";

        var project = DemoSession.CurrentProject;
        var projectId = string.IsNullOrWhiteSpace(project?.MotorId) ? "2025DJXXXX" : project!.MotorId;
        HeroStatusText.Text = $"测试系统已就绪 · 项目 {projectId} · 3/21 试验";
        ProgressCountText.Text = "3 / 21 项";
    }

    private void RefreshDemoMetrics()
    {
        var voltage = 379.4 + _random.NextDouble() * 1.4;
        var current = 12.20 + _random.NextDouble() * 0.55;
        var power = 7080 + _random.Next(0, 160);
        var speed = 1482 + _random.Next(0, 9);
        var pf = 0.845 + _random.NextDouble() * 0.03;
        var efficiency = 88.8 + _random.NextDouble() * 1.6;

        MetricVoltageValue.Text = $"{voltage:F1} V";
        MetricCurrentValue.Text = $"{current:F2} A";
        MetricPowerValue.Text = $"{power:F0} W";
        MetricSpeedValue.Text = $"{speed} rpm";
        MetricPfValue.Text = $"{pf:F2}";
        MetricPowerHint.Text = $"效率 {efficiency:F1}%";
    }

    private void NewTest_Click(object sender, RoutedEventArgs e)
    {
        Shell?.Navigate("new-test");
    }

    private void ContinueTest_Click(object sender, RoutedEventArgs e)
    {
        DemoSession.CurrentProject ??= DemoSession.CreateDemoDefaults();
        Shell?.Navigate("project");
        Shell?.ShowToast("已继续当前试验");
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        Shell?.Navigate("project");
        Shell?.ShowToast("已打开试验项目");
    }

    private void OpenRealtime_Click(object sender, RoutedEventArgs e)
    {
        Shell?.Navigate("realtime");
        Shell?.ShowToast("已打开实时数据");
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        Shell?.Navigate("report");
        Shell?.ShowToast("已打开报表输出");
    }
}
