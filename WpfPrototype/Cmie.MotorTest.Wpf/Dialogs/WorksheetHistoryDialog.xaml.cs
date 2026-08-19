using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Dialogs;

public partial class WorksheetHistoryDialog : Window
{
    public WorksheetHistoryDialog(IEnumerable<WorksheetRecordSummary> records)
    {
        InitializeComponent();
        Records = new ObservableCollection<WorksheetRecordSummary>(records);
        DataContext = this;
        RecordsGrid.MouseDoubleClick += RecordsGrid_MouseDoubleClick;
        if (Records.Count > 0) RecordsGrid.SelectedIndex = 0;
    }

    public ObservableCollection<WorksheetRecordSummary> Records { get; }
    public WorksheetRecordSummary? SelectedRecord => RecordsGrid.SelectedItem as WorksheetRecordSummary;
    public string SelectedAction { get; private set; } = string.Empty;

    private void Load_Click(object sender, RoutedEventArgs e) => Complete("load");
    private void Delete_Click(object sender, RoutedEventArgs e) => Complete("delete");
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void RecordsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Complete("load");

    private void Complete(string action)
    {
        if (SelectedRecord is null) return;
        SelectedAction = action;
        DialogResult = true;
    }
}
