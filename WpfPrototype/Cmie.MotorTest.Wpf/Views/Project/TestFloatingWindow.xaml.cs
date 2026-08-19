using System.Windows;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Cmie.MotorTest.Wpf.Models;
using Cmie.MotorTest.Wpf.Services;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class TestFloatingWindow : UserControl
{
    private Point _pointerStart;
    private double _startLeft;
    private double _startTop;
    private double _startWidth;
    private double _startHeight;
    private bool _dragging;
    private bool _resizing;
    private string _resizeDirection = string.Empty;
    private UIElement? _resizeCapture;
    private readonly Dictionary<string, TextBlock> _generatedSummaryValues = new();
    private readonly double _ratedVoltage;
    private readonly double _ratedFrequency;
    private readonly double _ratedPowerWatts;
    private readonly int _poles;

    public TestFloatingWindow(TestItem item, TestProjectInfo project)
    {
        InitializeComponent();
        TestKey = item.Key;
        TestTitle = item.Title;
        ProjectId = project.MotorId;
        _ratedVoltage = ParseProjectNumber(project.Voltage, 380);
        _ratedFrequency = ParseProjectNumber(project.Frequency, 50);
        _ratedPowerWatts = ParseProjectNumber(project.Power, 11) * 1000;
        _poles = (int)ParseProjectNumber(project.Poles, 4);
        TitleText.Text = item.Title;
        BodyTitleText.Text = item.Title;
        ProjectText.Text = $"项目 {project.MotorId} · {project.Model} · {project.Power} kW";
        ResistanceProjectText.Text = $"项目 {project.MotorId} · {project.Model} · {project.Power} kW · 380 V";
        var isResistanceTest = item.Key == "resistance";
        var isGeneratedTest = item.Key is "noload" or "load" or "method-a";
        ResistancePanel.Visibility = isResistanceTest ? Visibility.Visible : Visibility.Collapsed;
        ResistanceTools.Visibility = isResistanceTest ? Visibility.Visible : Visibility.Collapsed;
        GeneratedPanel.Visibility = isGeneratedTest ? Visibility.Visible : Visibility.Collapsed;
        GeneratedTools.Visibility = isGeneratedTest ? Visibility.Visible : Visibility.Collapsed;
        GenericPanel.Visibility = isResistanceTest || isGeneratedTest ? Visibility.Collapsed : Visibility.Visible;
        GenericTools.Visibility = isResistanceTest || isGeneratedTest ? Visibility.Collapsed : Visibility.Visible;
        if (isGeneratedTest)
        {
            BuildGeneratedWorksheet(item.Key, item.Title, project);
        }
        HintText.Text = item.Key switch
        {
            "resistance" => "绕组温度 T1：25.0 ℃ · 额定折算温度 Tref：75.0 ℃\n当前为直流电阻演示数据表。",
            "noload" => "在额定频率下调节电压，记录空载电流、输入功率和功率因数。",
            _ => "该试验的专用数据表将在后续业务接入时细化；当前保留同屏布局与完整浮窗交互。"
        };
    }

    public string TestKey { get; }
    public string TestTitle { get; }
    public string ProjectId { get; }
    public bool IsMaximized { get; private set; }
    public NoLoadCalculationResult? LastNoLoadResult { get; private set; }

    public event Action<TestFloatingWindow>? FocusRequested;
    public event Action<TestFloatingWindow>? MinimizeRequested;
    public event Action<TestFloatingWindow>? CloseRequested;
    public event Action<TestFloatingWindow>? MaximizeRequested;
    public event Action<TestFloatingWindow, string>? ToolInvoked;

    public WorksheetDocument CaptureWorksheet() => new(
        ProjectId,
        TestKey,
        TestTitle,
        FindTextBoxes(ActiveWorksheetRoot()).Select(box => box.Text).ToArray(),
        DateTimeOffset.Now,
        TestKey == "resistance" ? CaptureResistanceFields() : null);

    public void ApplyWorksheet(WorksheetDocument worksheet)
    {
        var boxes = FindTextBoxes(ActiveWorksheetRoot()).ToArray();
        for (var index = 0; index < boxes.Length; index++)
            boxes[index].Text = index < worksheet.Values.Count ? worksheet.Values[index] : string.Empty;

        if (TestKey == "resistance")
        {
            ApplyResistanceFields(worksheet.Fields);
            try
            {
                CalculateResistance();
            }
            catch (ArgumentException)
            {
                ResetResistanceOutputs();
            }
        }
    }

    public void ClearWorksheet()
    {
        foreach (var box in FindTextBoxes(ActiveWorksheetRoot())) box.Clear();
        if (TestKey == "resistance") ResetResistanceOutputs();
    }

    public string CalculateResistance()
    {
        if (TestKey != "resistance")
        {
            throw new InvalidOperationException("当前试验不是电阻测量试验。");
        }

        var result = ResistanceCalculationService.Calculate(new ResistanceCalculationInput(
            ParsePositiveResistance(StatorRabBox.Text, "R U-V"),
            ParsePositiveResistance(StatorRbcBox.Text, "R V-W"),
            ParsePositiveResistance(StatorRcaBox.Text, "R W-U"),
            25.0,
            75.0,
            WindingConnection.Star));

        StatorRabRefText.Text = FormatNumber(result.LineAtReference.PhaseA);
        StatorRbcRefText.Text = FormatNumber(result.LineAtReference.PhaseB);
        StatorRcaRefText.Text = FormatNumber(result.LineAtReference.PhaseC);
        StatorRaRefBox.Text = FormatNumber(result.PhaseAtReference.PhaseA);
        StatorRbRefBox.Text = FormatNumber(result.PhaseAtReference.PhaseB);
        StatorRcRefBox.Text = FormatNumber(result.PhaseAtReference.PhaseC);
        StatorUnbalanceText.Text = $"{result.UnbalancePercent:0.00000} %";

        return $"定子电阻已折算至 75 ℃ · 平均相电阻 {FormatNumber(result.AveragePhaseResistance)} Ω · 不平衡度 {result.UnbalancePercent:0.00000} %";
    }

    public string CalculateNoLoad()
    {
        if (TestKey != "noload") throw new InvalidOperationException("当前试验不是空载特性试验。 ");
        var result = NoLoadCalculationService.Calculate(CaptureWorksheet(), 380, 0.842);
        LastNoLoadResult = result;
        var boxes = FindTextBoxes(GeneratedContent).ToArray();
        foreach (var point in result.Points)
        {
            var offset = point.RowIndex * 12;
            if (offset + 10 >= boxes.Length) continue;
            boxes[offset + 3].Text = point.Voltage.ToString("F2", CultureInfo.InvariantCulture);
            boxes[offset + 7].Text = point.Current.ToString("F3", CultureInfo.InvariantCulture);
            boxes[offset + 10].Text = point.PowerFactor.ToString("F4", CultureInfo.InvariantCulture);
        }
        SetGeneratedSummary("额定空载电流 I0", $"{result.RatedCurrent:F3} A");
        SetGeneratedSummary("额定空载损耗 P0", $"{result.RatedInputPower:F1} W");
        SetGeneratedSummary("额定机械损耗 Pfw", $"{result.MechanicalLoss:F1} W");
        SetGeneratedSummary("额定铁耗 Pfe", $"{result.IronLoss:F1} W");
        return $"空载计算完成 · {result.Points.Count} 点 · I0 {result.RatedCurrent:F3} A · P0 {result.RatedInputPower:F1} W · Pfw {result.MechanicalLoss:F1} W · Pfe {result.IronLoss:F1} W";
    }

    public string CalculateMethodA(NoLoadCalculationResult noLoad)
    {
        if (TestKey != "method-a") throw new InvalidOperationException("当前试验不是 A 法数据分析。 ");
        var result = MethodACalculationService.Calculate(
            CaptureWorksheet(), noLoad, _ratedVoltage, _ratedFrequency, _poles, _ratedPowerWatts, 0.842, 25);
        var boxes = FindTextBoxes(GeneratedContent).ToArray();
        foreach (var point in result.Points)
        {
            SetMatrixValue(boxes, 5, point.PointIndex, point.SynchronousSpeed, "F1");
            SetMatrixValue(boxes, 7, point.PointIndex, point.Slip, "F2");
            SetMatrixValue(boxes, 8, point.PointIndex, point.SlipPercent, "F3");
            SetMatrixValue(boxes, 9, point.PointIndex, point.CorrectedSlip, "F2");
            SetMatrixValue(boxes, 10, point.PointIndex, point.CorrectedSpeed, "F1");
            SetMatrixValue(boxes, 12, point.PointIndex, point.CorrectedTorque, "F3");
            SetMatrixValue(boxes, 13, point.PointIndex, point.CorrectedOutputPower / 1000, "F4");
            SetMatrixValue(boxes, 14, point.PointIndex, point.IronLoss, "F2");
            SetMatrixValue(boxes, 15, point.PointIndex, point.StatorCopperLoss, "F2");
            SetMatrixValue(boxes, 16, point.PointIndex, point.CorrectedStatorCopperLoss, "F2");
            SetMatrixValue(boxes, 17, point.PointIndex, point.StatorCopperLossDelta, "F2");
            SetMatrixValue(boxes, 18, point.PointIndex, point.RotorCopperLoss, "F2");
            SetMatrixValue(boxes, 19, point.PointIndex, point.CorrectedRotorCopperLoss, "F2");
            SetMatrixValue(boxes, 20, point.PointIndex, point.RotorCopperLossDelta, "F2");
            SetMatrixValue(boxes, 21, point.PointIndex, point.CorrectedInputPower, "F2");
            SetMatrixValue(boxes, 22, point.PointIndex, point.MechanicalLoss, "F2");
            SetMatrixValue(boxes, 23, point.PointIndex, point.StrayLoss, "F2");
            SetMatrixValue(boxes, 24, point.PointIndex, point.Efficiency, "F3");
            SetMatrixValue(boxes, 25, point.PointIndex, point.PowerFactor, "F4");
        }
        var rated = result.RatedPoint;
        SetGeneratedSummary("定子电流 Stator I", $"{rated.Current:F3} A");
        SetGeneratedSummary("效率 Effect", $"{rated.Efficiency:F3} %");
        SetGeneratedSummary("转差率 S0", $"{rated.SlipPercent:F3} %");
        SetGeneratedSummary("功率因数 Cosφ", rated.PowerFactor.ToString("F4"));
        SetGeneratedSummary("定子损耗 Pcu1", $"{rated.CorrectedStatorCopperLoss:F1} W");
        SetGeneratedSummary("转子损耗 Pcu2", $"{rated.CorrectedRotorCopperLoss:F1} W");
        SetGeneratedSummary("杂散损耗 Ps", $"{rated.StrayLoss:F1} W");
        SetGeneratedSummary("机械损耗 Pfw", $"{rated.MechanicalLoss:F1} W");
        SetGeneratedSummary("铁耗 Pfe", $"{rated.IronLoss:F1} W");
        return $"A 法分析完成 · {result.Points.Count} 点 · 额定邻近点效率 {rated.Efficiency:F2}% · 功率因数 {rated.PowerFactor:F3}";
    }

    private DependencyObject ActiveWorksheetRoot() => TestKey switch
    {
        "resistance" => ResistancePanel,
        "noload" or "load" or "method-a" => GeneratedPanel,
        _ => GenericPanel
    };

    private static IEnumerable<TextBox> FindTextBoxes(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBox box) yield return box;
            foreach (var descendant in FindTextBoxes(child)) yield return descendant;
        }
    }

    private static double ParsePositiveResistance(string text, string fieldName)
    {
        var valueText = text.Trim();
        var parsed = double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : double.TryParse(valueText, NumberStyles.Float, CultureInfo.CurrentCulture, out var local)
                ? local
                : double.NaN;
        if (double.IsNaN(parsed) || parsed <= 0)
        {
            throw new ArgumentException($"请填写有效的定子线电阻 {fieldName}。");
        }

        return parsed;
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private IReadOnlyDictionary<string, string> CaptureResistanceFields() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["定子接法"] = "Y",
            ["绕组温度"] = "25.0",
            ["规定折算温度"] = "75.0",
            ["定子UV直流电阻初始值"] = StatorRabBox.Text,
            ["定子VW直流电阻初始值"] = StatorRbcBox.Text,
            ["定子WU直流电阻初始值"] = StatorRcaBox.Text,
            ["转子KL直流电阻测量值"] = RotorRabBox.Text,
            ["转子LM直流电阻测量值"] = RotorRbcBox.Text,
            ["转子MK直流电阻测量值"] = RotorRcaBox.Text,
            ["定子UV直流电阻折算值"] = StatorRabRefText.Text,
            ["定子VW直流电阻折算值"] = StatorRbcRefText.Text,
            ["定子WU直流电阻折算值"] = StatorRcaRefText.Text,
            ["定子U直流电阻折算值"] = StatorRaRefBox.Text,
            ["定子V直流电阻折算值"] = StatorRbRefBox.Text,
            ["定子W直流电阻折算值"] = StatorRcRefBox.Text,
            ["定子绕组不平衡度"] = StatorUnbalanceText.Text
        };

    private void ApplyResistanceFields(IReadOnlyDictionary<string, string>? fields)
    {
        if (fields is null || fields.Count == 0) return;

        SetText(StatorRabBox, fields, "定子UV直流电阻初始值");
        SetText(StatorRbcBox, fields, "定子VW直流电阻初始值");
        SetText(StatorRcaBox, fields, "定子WU直流电阻初始值");
        SetText(RotorRabBox, fields, "转子KL直流电阻测量值");
        SetText(RotorRbcBox, fields, "转子LM直流电阻测量值");
        SetText(RotorRcaBox, fields, "转子MK直流电阻测量值");
    }

    private static void SetText(
        TextBox target,
        IReadOnlyDictionary<string, string> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value)) target.Text = value;
    }

    private void ResetResistanceOutputs()
    {
        StatorRabRefText.Text = "—";
        StatorRbcRefText.Text = "—";
        StatorRcaRefText.Text = "—";
        RotorRabRefText.Text = "—";
        RotorRbcRefText.Text = "—";
        RotorRcaRefText.Text = "—";
        StatorRaRefBox.Clear();
        StatorRbRefBox.Clear();
        StatorRcRefBox.Clear();
        RotorRaRefBox.Clear();
        RotorRbRefBox.Clear();
        RotorRcRefBox.Clear();
        StatorUnbalanceText.Text = "—";
        RotorUnbalanceBox.Clear();
    }

    private void BuildGeneratedWorksheet(string key, string title, TestProjectInfo project)
    {
        GeneratedContent.Children.Clear();
        _generatedSummaryValues.Clear();
        GeneratedContent.Children.Add(CreateWorksheetHeader(title, project));
        GeneratedContent.Children.Add(CreateNameplateCard());

        switch (key)
        {
            case "noload":
                BuildNoLoadWorksheet();
                ConfigureGeneratedTools(
                    ("▧  表格清空", "表格清空"),
                    ("↧  参数导入", "参数导入"),
                    ("▣  读取数据", "读取数据"),
                    ("●  数据保存", "数据保存"),
                    ("∑  结果计算", "结果计算"),
                    ("⇩  导出记录", "导出记录"),
                    ("▤  预览打印", "预览打印"));
                break;
            case "load":
                BuildLoadWorksheet();
                ConfigureGeneratedTools(
                    ("↧  参数导入", "参数导入"),
                    ("▣  读取数据", "读取数据"),
                    ("✂  删除记录", "删除记录"),
                    ("□  数据调用", "数据调用"),
                    ("●  数据保存", "数据保存"),
                    ("▤  预览打印", "预览打印"));
                break;
            case "method-a":
                BuildMethodAWorksheet();
                ConfigureGeneratedTools(
                    ("▧  表格清空", "表格清空"),
                    ("↧  参数导入", "参数导入"),
                    ("□  数据调用", "数据调用"),
                    ("✂  删除粗差", "删除粗差"),
                    ("▤  原始记录", "原始记录"),
                    ("●  数据保存", "数据保存"),
                    ("∑  数据分析", "数据分析"),
                    ("▣  预览打印", "预览打印"));
                break;
        }
    }

    private UIElement CreateWorksheetHeader(string title, TestProjectInfo project)
    {
        var root = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = $"项目 {project.MotorId} · {project.Model} · {project.Power} kW · 380 V",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("MutedBrush")
        });
        root.Children.Add(text);
        var state = new Border
        {
            Background = (Brush)FindResource("AccentSoftBrush"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "●  数据就绪", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") }
        };
        Grid.SetColumn(state, 1);
        root.Children.Add(state);
        return root;
    }

    private UIElement CreateNameplateCard()
    {
        var values = new (string Label, string Value)[]
        {
            ("型号 Type", "Y2-160M-4"), ("编号 ID", "2025DJXXXX"), ("电压 Voltage", "380 V"),
            ("电流 Current", "15.0 A"), ("功率 Power", "11 kW"), ("转速 Speed", "1460 rpm"),
            ("频率 Freq", "50 Hz"), ("功率因数 PF", "0.86"), ("接线 Wire", "Y"), ("绝缘 Ins.", "F")
        };
        var fields = new UniformGrid { Columns = 5 };
        foreach (var (label, value) in values)
        {
            var field = new StackPanel { Margin = new Thickness(5, 3, 5, 3) };
            field.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = (Brush)FindResource("MutedBrush") });
            field.Children.Add(new TextBlock { Text = value, Margin = new Thickness(0, 3, 0, 0), FontSize = 11, FontWeight = FontWeights.SemiBold });
            fields.Children.Add(field);
        }
        return new Border
        {
            Style = (Style)FindResource("ResistanceSectionStyle"),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12),
            Child = fields
        };
    }

    private void BuildNoLoadWorksheet()
    {
        GeneratedContent.Children.Add(CreateSectionTitle("空载特性试验", "No Load Test"));
        var headers = new[]
        {
            "U-V\n(V)", "V-W\n(V)", "W-U\n(V)", "Uavg\n(V)",
            "I-U\n(A)", "I-V\n(A)", "I-W\n(A)", "Iavg\n(A)",
            "P0\n(W)", "Freq\n(Hz)", "Cosφ", "T0\n(℃)"
        };
        GeneratedContent.Children.Add(CreateBlankMatrix(headers, 14));
        GeneratedContent.Children.Add(CreateFieldBand(new (string, string)[]
        {
            ("定子初始温度 θ1", "25.0 ℃"), ("定子初始线电阻 R1", "0.842 Ω"),
            ("试验后定子线电阻 R0", "—"), ("额定空载电流 I0", "— A"),
            ("额定空载损耗 P0", "— W"), ("额定机械损耗 Pfw", "— W"), ("额定铁耗 Pfe", "— W")
        }, trackValues: true));
    }

    private void BuildLoadWorksheet()
    {
        GeneratedContent.Children.Add(CreateSectionTitle("负载特性", "Load characteristics of motor"));
        var rows = new[]
        {
            "转速 Speed (rpm)", "轴转矩 Torque (Nm)", "轴功率 Shaft Power (W)",
            "电压 U-V (V)", "电压 V-W (V)", "电压 W-U (V)", "平均电压 Uavg (V)",
            "电流 U (A)", "电流 V (A)", "电流 W (A)", "平均电流 Iavg (A)",
            "功率 P1 (W)", "功率因数 Cosφ", "频率 Frequency (Hz)", "绕组最高温度 θt (℃)"
        };
        GeneratedContent.Children.Add(CreateMetricMatrix(rows, 12));
        GeneratedContent.Children.Add(CreateFieldBand(new (string, string)[]
        {
            ("试验电机相关数据", "负载工况"), ("初始温度 θ1", "25.0 ℃"),
            ("定子初始线电阻 R1", "0.842 Ω"), ("试验后定子线电阻 R0", "—")
        }));
    }

    private void BuildMethodAWorksheet()
    {
        GeneratedContent.Children.Add(CreateSectionTitle("A 法负载特性分析", "Load characteristics analysis · A mode"));
        GeneratedContent.Children.Add(CreateFieldBand(new (string, string)[]
        {
            ("定子初始阻值(冷) R1", "0.842 Ω"), ("测量 R1 温度 θ1", "25.0 ℃"),
            ("负载试验冷却介质温度 θa", "26.4 ℃"), ("转矩修正值 Kd", "0.00 Nm")
        }));
        var rows = new[]
        {
            "绕组温度 θt (℃)", "线电压 Line U (V)", "线电流 Line I (A)", "输入功率 P1 (W)",
            "频率 f (Hz)", "同步转速 Speed (rpm)", "转速 Speed (rpm)", "转差 s (rpm)",
            "转差率 S (%)", "转差修正 s Fix (rpm)", "转速修正 Speed Fix (rpm)",
            "轴转矩 TQ (Nm)", "轴转矩修正 TQ Fix (Nm)", "修正输出功率 P2c (kW)",
            "铁耗 Pfe (W)", "定子损耗 Pcu1 (W)", "修正定子损耗 Pcu1c (W)",
            "定子损耗增量 ΔPcu1 (W)", "转子损耗 Pcu2 (W)", "修正转子损耗 Pcu2c (W)",
            "转子损耗增量 ΔPcu2 (W)", "修正定子输入功率 P1c (W)", "风摩耗 Pfw (W)",
            "杂散损耗 Ps (W)", "效率 Effect (%)", "功率因数 Cosφ"
        };
        GeneratedContent.Children.Add(CreateMetricMatrix(rows, 12));
        GeneratedContent.Children.Add(CreateRatedSummary());
        var fitBox = new Border { Style = (Style)FindResource("ResistanceSectionStyle"), Padding = new Thickness(12), Margin = new Thickness(0, 12, 0, 0) };
        var fit = new StackPanel { Orientation = Orientation.Horizontal };
        fit.Children.Add(new TextBlock { Text = "数据拟合方式", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 18, 0) });
        fit.Children.Add(new RadioButton { Content = "高阶拟合", GroupName = "FitMode", Margin = new Thickness(0, 0, 18, 0) });
        fit.Children.Add(new RadioButton { Content = "低阶拟合", GroupName = "FitMode", IsChecked = true });
        fitBox.Child = fit;
        GeneratedContent.Children.Add(fitBox);
    }

    private UIElement CreateSectionTitle(string title, string subtitle)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 0, 0, 7) };
        panel.Children.Add(new Border { Width = 3, Height = 14, CornerRadius = new CornerRadius(2), Background = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0) });
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"  {subtitle}", FontSize = 10, Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private UIElement CreateBlankMatrix(IReadOnlyList<string> headers, int rowCount)
    {
        var grid = new Grid { MinWidth = 880 };
        foreach (var _ in headers) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        for (var row = 0; row < rowCount; row++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });
        for (var column = 0; column < headers.Count; column++) AddTableCell(grid, 0, column, headers[column], true);
        for (var row = 1; row <= rowCount; row++)
            for (var column = 0; column < headers.Count; column++) AddTableCell(grid, row, column, string.Empty, false, true);
        return WrapTable(grid);
    }

    private UIElement CreateMetricMatrix(IReadOnlyList<string> rowLabels, int pointCount)
    {
        var grid = new Grid { MinWidth = 900 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        for (var column = 0; column < pointCount; column++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var _ in rowLabels) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });
        for (var row = 0; row < rowLabels.Count; row++)
        {
            AddTableCell(grid, row, 0, rowLabels[row], true);
            for (var column = 1; column <= pointCount; column++) AddTableCell(grid, row, column, string.Empty, false, true);
        }
        return WrapTable(grid);
    }

    private UIElement WrapTable(Grid grid) => new Border
    {
        Style = (Style)FindResource("ResistanceSectionStyle"),
        ClipToBounds = true,
        Child = grid
    };

    private void AddTableCell(Grid grid, int row, int column, string text, bool header, bool editable = false)
    {
        var cell = new Border { Style = (Style)FindResource(header ? "ResistanceHeaderCellStyle" : "ResistanceCellStyle") };
        cell.Child = editable
            ? new TextBox { Text = text, Style = (Style)FindResource("ResistanceInputStyle") }
            : new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource(header ? "ResistanceHeaderTextStyle" : "ResistanceCellTextStyle") };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private UIElement CreateFieldBand(IReadOnlyList<(string Label, string Value)> fields, bool trackValues = false)
    {
        var layout = new UniformGrid { Columns = Math.Min(4, fields.Count) };
        foreach (var (label, value) in fields)
        {
            var field = new StackPanel { Margin = new Thickness(8, 4, 8, 4) };
            field.Children.Add(new TextBlock { Text = label, FontSize = 9.5, Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap });
            var valueText = new TextBlock { Text = value, Margin = new Thickness(0, 3, 0, 0), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("AccentBrush") };
            field.Children.Add(valueText);
            if (trackValues) _generatedSummaryValues[label] = valueText;
            layout.Children.Add(field);
        }
        return new Border { Style = (Style)FindResource("ResistanceSectionStyle"), Padding = new Thickness(8), Margin = new Thickness(0, 10, 0, 0), Child = layout };
    }

    private void SetGeneratedSummary(string label, string value)
    {
        if (_generatedSummaryValues.TryGetValue(label, out var text)) text.Text = value;
    }

    private static void SetMatrixValue(IReadOnlyList<TextBox> boxes, int row, int point, double value, string format)
    {
        var index = row * 12 + point;
        if (index < boxes.Count) boxes[index].Text = value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static double ParseProjectNumber(string value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : fallback;

    private UIElement CreateRatedSummary()
    {
        var fields = new (string, string)[]
        {
            ("定子电流 Stator I", "— A"), ("效率 Effect", "— %"), ("转差率 S0", "— %"), ("功率因数 Cosφ", "—"),
            ("定子损耗 Pcu1", "— W"), ("转子损耗 Pcu2", "— W"), ("杂散损耗 Ps", "— W"),
            ("机械损耗 Pfw", "— W"), ("铁耗 Pfe", "— W")
        };
        return CreateFieldBand(fields, trackValues: true);
    }

    private void ConfigureGeneratedTools(params (string Label, string Action)[] tools)
    {
        GeneratedTools.Children.Clear();
        GeneratedTools.Children.Add(new TextBlock
        {
            Text = "试验操作",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
            FontSize = 10.5,
            Foreground = (Brush)FindResource("MutedBrush")
        });
        foreach (var (label, action) in tools)
        {
            var button = new Button { Content = label, Tag = action, Style = (Style)FindResource("ResistanceToolStyle") };
            button.Click += Tool_Click;
            GeneratedTools.Children.Add(button);
        }
        var exit = new Button { Content = "×  退出操作", Style = (Style)FindResource("ResistanceToolStyle"), Foreground = (Brush)FindResource("BadBrush"), Margin = new Thickness(0, 8, 0, 0) };
        exit.Click += Close_Click;
        GeneratedTools.Children.Add(exit);
    }

    private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => FocusRequested?.Invoke(this);

    private void Minimize_Click(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke(this);

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this);

    private void Maximize_Click(object sender, RoutedEventArgs e) => MaximizeRequested?.Invoke(this);

    public void SetMaximized(bool maximized)
    {
        IsMaximized = maximized;
        MaximizeButton.Content = maximized ? "❐" : "□";
        MaximizeButton.ToolTip = maximized ? "还原" : "最大化";
        ResizeHandles.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string action })
        {
            ToolInvoked?.Invoke(this, action);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            MaximizeRequested?.Invoke(this);
            e.Handled = true;
            return;
        }

        if (IsMaximized)
        {
            return;
        }

        FocusRequested?.Invoke(this);
        _dragging = true;
        _pointerStart = e.GetPosition(Parent as UIElement);
        _startLeft = Canvas.GetLeft(this);
        _startTop = Canvas.GetTop(this);
        TitleBar.CaptureMouse();
        e.Handled = true;
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Parent is not Canvas canvas)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        var left = Math.Clamp(_startLeft + point.X - _pointerStart.X, -40, Math.Max(-40, canvas.ActualWidth - 100));
        var top = Math.Clamp(_startTop + point.Y - _pointerStart.Y, 0, Math.Max(0, canvas.ActualHeight - 42));
        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        TitleBar.ReleaseMouseCapture();
    }

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsMaximized
            || Parent is not Canvas canvas
            || sender is not FrameworkElement { Tag: string direction } handle)
        {
            return;
        }

        FocusRequested?.Invoke(this);
        _resizing = true;
        _pointerStart = e.GetPosition(canvas);
        _startLeft = Canvas.GetLeft(this);
        _startTop = Canvas.GetTop(this);
        _startWidth = ActualWidth;
        _startHeight = ActualHeight;
        _resizeDirection = direction;
        _resizeCapture = handle;
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || Parent is not Canvas canvas)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        var deltaX = point.X - _pointerStart.X;
        var deltaY = point.Y - _pointerStart.Y;
        var left = _startLeft;
        var top = _startTop;
        var width = _startWidth;
        var height = _startHeight;

        if (_resizeDirection.Contains('L'))
        {
            width = Math.Clamp(_startWidth - deltaX, MinWidth, Math.Max(MinWidth, _startWidth + _startLeft));
            left = _startLeft + _startWidth - width;
        }
        else if (_resizeDirection.Contains('R'))
        {
            width = Math.Clamp(_startWidth + deltaX, MinWidth, Math.Max(MinWidth, canvas.ActualWidth - _startLeft));
        }

        if (_resizeDirection.Contains('T'))
        {
            height = Math.Clamp(_startHeight - deltaY, MinHeight, Math.Max(MinHeight, _startHeight + _startTop));
            top = _startTop + _startHeight - height;
        }
        else if (_resizeDirection.Contains('B'))
        {
            height = Math.Clamp(_startHeight + deltaY, MinHeight, Math.Max(MinHeight, canvas.ActualHeight - _startTop));
        }

        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
        Width = width;
        Height = height;
    }

    private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _resizing = false;
        _resizeCapture?.ReleaseMouseCapture();
        _resizeCapture = null;
        _resizeDirection = string.Empty;
    }
}
