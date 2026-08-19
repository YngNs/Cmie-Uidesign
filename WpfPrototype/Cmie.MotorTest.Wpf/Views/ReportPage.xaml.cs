using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Cmie.MotorTest.Wpf.Views;

public partial class ReportPage : UserControl
{
    public ReportPage()
    {
        InitializeComponent();
        Reports = new ObservableCollection<ReportRow>
        {
            new("封面", "基础信息", true),
            new("产品规格", "基础信息", true),
            new("数据汇总表", "基础信息", true),
            new("电阻测定", "基础试验", false),
            new("噪声和振动测定", "其他试验", false),
            new("空载试验", "基础试验", true),
            new("堵转试验", "基础试验", false),
            new("负载试验记录", "负载试验", true),
            new("A 法负载试验分析", "效率分析", false),
            new("B 法负载试验分析", "效率分析", false),
            new("E 法负载试验分析", "效率分析", false),
            new("温升试验数据", "温升试验", false),
            new("热电阻温升推导", "温升试验", false),
            new("圆图法最大转矩计算", "其他试验", false)
        };
        SelectedReports = new ObservableCollection<SelectedReportItem>();
        DataContext = this;
        Loaded += (_, _) => RebuildSelectionOrder();
    }

    public ObservableCollection<ReportRow> Reports { get; }
    public ObservableCollection<SelectedReportItem> SelectedReports { get; }
    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void SetAll(bool selected)
    {
        foreach (var row in Reports) row.IsSelected = selected;
        RebuildSelectionOrder();
    }

    private void RebuildSelectionOrder()
    {
        SelectedReports.Clear();
        foreach (var row in Reports.Where(row => row.IsSelected))
            SelectedReports.Add(new SelectedReportItem(SelectedReports.Count + 1, row));
        UpdateSummary();
    }

    private void SyncSelection(ReportRow row)
    {
        var existing = SelectedReports.FirstOrDefault(item => ReferenceEquals(item.Source, row));
        if (row.IsSelected && existing is null)
            SelectedReports.Add(new SelectedReportItem(SelectedReports.Count + 1, row));
        else if (!row.IsSelected && existing is not null)
            SelectedReports.Remove(existing);

        for (var index = 0; index < SelectedReports.Count; index++)
            SelectedReports[index].Index = index + 1;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (SummaryText is null) return;
        SummaryText.Text = $"已选 {SelectedReports.Count}";
        EmptySelectionText.Visibility = SelectedReports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
    private void SelectNone_Click(object sender, RoutedEventArgs e) => SetAll(false);
    private void ReportCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: ReportRow row }) SyncSelection(row);
    }

    private void Export_Click(object sender, RoutedEventArgs e) =>
        Shell?.ShowToast(SelectedReports.Count == 0 ? "请至少选择一个报告章节" : $"已按顺序提交 {SelectedReports.Count} 个章节到 Excel（演示）");

    private void CloseExcel_Click(object sender, RoutedEventArgs e) => Shell?.ShowToast("已关闭 Excel 进程（演示）");

    public sealed class ReportRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        public ReportRow(string name, string category, bool selected) { Name = name; Category = category; _isSelected = selected; }
        public string Name { get; }
        public string Category { get; }
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed class SelectedReportItem : INotifyPropertyChanged
    {
        private int _index;
        public SelectedReportItem(int index, ReportRow source) { _index = index; Source = source; }
        public ReportRow Source { get; }
        public string Name => Source.Name;
        public string Category => Source.Category;
        public int Index { get => _index; set { if (_index == value) return; _index = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Index))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
