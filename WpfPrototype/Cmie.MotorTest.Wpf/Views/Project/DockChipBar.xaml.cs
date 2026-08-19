using System.Windows;
using System.Windows.Controls;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class DockChipBar : UserControl
{
    public DockChipBar()
    {
        InitializeComponent();
    }

    public event Action? ExpandTreeRequested;
    public event Action? ExpandMetricsRequested;

    public void SetCollapsedPanels(bool treeCollapsed, bool metricsCollapsed)
    {
        TreeChip.Visibility = treeCollapsed ? Visibility.Visible : Visibility.Collapsed;
        MetricsChip.Visibility = metricsCollapsed ? Visibility.Visible : Visibility.Collapsed;
        Visibility = treeCollapsed || metricsCollapsed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TreeChip_Click(object sender, RoutedEventArgs e) => ExpandTreeRequested?.Invoke();

    private void MetricsChip_Click(object sender, RoutedEventArgs e) => ExpandMetricsRequested?.Invoke();
}
