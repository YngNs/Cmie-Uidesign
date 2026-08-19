using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Cmie.MotorTest.Wpf.Demo;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views;

public partial class RealtimePage : UserControl
{
    private readonly List<MetricReading> _allMetrics = DemoProjectData.CreateMetrics();
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private bool _frozen;

    public RealtimePage()
    {
        InitializeComponent();
        MeasurementMetrics = _allMetrics.Where(metric => metric.Group == "测量数据").ToList();
        ShaftMetrics = _allMetrics.Where(metric => metric.Group == "轴功率").ToList();
        TemperatureMetrics = _allMetrics.Where(metric => metric.Group.StartsWith("温度", StringComparison.Ordinal)).ToList();
        PinnedMetrics = new ObservableCollection<MetricReading>(_allMetrics.Where(metric => metric.IsPinned));
        PinnedBar.Items = PinnedMetrics;
        PinnedBar.RemoveRequested += TogglePin;
        PinnedBar.ClearRequested += ClearPins;
        DataContext = this;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _timer.Tick += (_, _) => RefreshValues();
        Loaded += (_, _) => { RefreshValues(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    public IReadOnlyList<MetricReading> MeasurementMetrics { get; }
    public IReadOnlyList<MetricReading> ShaftMetrics { get; }
    public IReadOnlyList<MetricReading> TemperatureMetrics { get; }
    public ObservableCollection<MetricReading> PinnedMetrics { get; }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void RefreshValues()
    {
        foreach (var metric in _allMetrics)
        {
            metric.Value = metric.Id switch
            {
                "u1" or "u2" or "u3" or "uavg" => (379.6 + _random.NextDouble()).ToString("F1"),
                "i1" or "i2" or "i3" or "iavg" => (12.2 + _random.NextDouble() * 0.5).ToString("F2"),
                "p" => (7760 + _random.Next(0, 130)).ToString(),
                "q" => (4.4 + _random.NextDouble() * 0.4).ToString("F2"),
                "f" => (49.98 + _random.NextDouble() * 0.06).ToString("F2"),
                "pf" => (0.84 + _random.NextDouble() * 0.04).ToString("F2"),
                "shaft" => (7090 + _random.Next(0, 120)).ToString(),
                "n" => (1482 + _random.Next(0, 9)).ToString(),
                "tq" => (45.4 + _random.NextDouble()).ToString("F1"),
                _ when metric.Unit == "°C" => (double.Parse(metric.Value) + (_random.NextDouble() - 0.5) * 0.2).ToString("F1"),
                _ => metric.Value
            };
        }

        SampleTimeText.Text = $"最近采样 {DateTime.Now:HH:mm:ss}";
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MetricReading metric })
        {
            TogglePin(metric);
        }
    }

    private void TogglePin(MetricReading metric)
    {
        metric.IsPinned = !metric.IsPinned;
        if (metric.IsPinned) PinnedMetrics.Add(metric); else PinnedMetrics.Remove(metric);
    }

    private void ClearPins()
    {
        foreach (var metric in _allMetrics) metric.IsPinned = false;
        PinnedMetrics.Clear();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshValues();
        Shell?.ShowToast("实时数据已刷新");
    }

    private void Freeze_Click(object sender, RoutedEventArgs e)
    {
        _frozen = !_frozen;
        if (_frozen) _timer.Stop(); else _timer.Start();
        FreezeButton.Content = _frozen ? "继续" : "冻结";
        Shell?.ShowToast(_frozen ? "已冻结实时数据" : "已恢复实时采样");
    }

    private void ClearPins_Click(object sender, RoutedEventArgs e)
    {
        ClearPins();
        Shell?.ShowToast("已清空重点指标");
    }
}
