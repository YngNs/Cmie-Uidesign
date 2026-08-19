using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Cmie.MotorTest.Wpf.Demo;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class CompactMetricsPanel : UserControl
{
    private readonly List<MetricReading> _allMetrics = DemoProjectData.CreateMetrics();

    public CompactMetricsPanel()
    {
        InitializeComponent();
        MeasurementMetrics = _allMetrics.Where(metric => metric.Group == "测量数据").ToList();
        ShaftMetrics = _allMetrics.Where(metric => metric.Group == "轴功率").ToList();
        TemperatureMetrics = _allMetrics.Where(metric => metric.Group.StartsWith("温度", StringComparison.Ordinal)).ToList();
        PinnedMetrics = new ObservableCollection<MetricReading>(_allMetrics.Where(metric => metric.IsPinned));
        DataContext = this;
    }

    public IReadOnlyList<MetricReading> MeasurementMetrics { get; }
    public IReadOnlyList<MetricReading> ShaftMetrics { get; }
    public IReadOnlyList<MetricReading> TemperatureMetrics { get; }
    public ObservableCollection<MetricReading> PinnedMetrics { get; }

    public event Action? CollapseRequested;

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MetricReading metric })
        {
            TogglePinnedMetric(metric);
        }
    }

    public void TogglePinnedMetric(MetricReading metric)
    {
        metric.IsPinned = !metric.IsPinned;
        if (metric.IsPinned)
        {
            PinnedMetrics.Add(metric);
        }
        else
        {
            PinnedMetrics.Remove(metric);
        }

    }

    public void ClearPinnedMetrics()
    {
        foreach (var metric in _allMetrics)
        {
            metric.IsPinned = false;
        }

        PinnedMetrics.Clear();
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke();
}
