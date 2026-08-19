using System.Windows;
using System.Windows.Controls;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views;

public partial class ProjectPage : UserControl
{
    private bool _treeCollapsed;
    private bool _metricsCollapsed;
    private TestProjectInfo _project = DemoSession.CreateDemoDefaults();

    public ProjectPage()
    {
        InitializeComponent();
        TestTree.TestSelected += item => Stage.Open(item, _project);
        TestTree.CollapseRequested += () => SetTreeCollapsed(true);
        MetricsPanel.CollapseRequested += () => SetMetricsCollapsed(true);
        DockBar.ExpandTreeRequested += () => SetTreeCollapsed(false);
        DockBar.ExpandMetricsRequested += () => SetMetricsCollapsed(false);
        HeaderPinnedBar.Items = MetricsPanel.PinnedMetrics;
        HeaderPinnedBar.RemoveRequested += MetricsPanel.TogglePinnedMetric;
        HeaderPinnedBar.ClearRequested += MetricsPanel.ClearPinnedMetrics;
        Stage.WindowStateChanged += (keys, activeKey) => TestTree.SetWindowState(keys, activeKey);
        Stage.StatusMessage += message => Shell?.ShowToast(message);
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    public void RefreshProject()
    {
        _project = DemoSession.CurrentProject ?? DemoSession.CreateDemoDefaults();
        ProjectIdText.Text = _project.MotorId;
    }

    private void ProjectPage_Loaded(object sender, RoutedEventArgs e) => RefreshProject();

    private void SetTreeCollapsed(bool collapsed)
    {
        _treeCollapsed = collapsed;
        TreeColumn.Width = collapsed ? new GridLength(0) : new GridLength(228);
        TreeGapColumn.Width = collapsed ? new GridLength(0) : new GridLength(10);
        TestTree.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SyncDockBar();
    }

    private void SetMetricsCollapsed(bool collapsed)
    {
        _metricsCollapsed = collapsed;
        MetricsColumn.Width = collapsed ? new GridLength(0) : new GridLength(270);
        MetricsGapColumn.Width = collapsed ? new GridLength(0) : new GridLength(10);
        MetricsPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SyncDockBar();
    }

    private void SyncDockBar()
    {
        var showDock = _treeCollapsed || _metricsCollapsed;
        DockColumn.Width = showDock ? new GridLength(50) : new GridLength(0);
        DockGapColumn.Width = showDock ? new GridLength(10) : new GridLength(0);
        DockBar.SetCollapsedPanels(_treeCollapsed, _metricsCollapsed);
    }
}
