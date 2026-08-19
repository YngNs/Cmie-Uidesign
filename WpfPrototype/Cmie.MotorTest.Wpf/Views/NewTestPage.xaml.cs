using System.Windows;
using System.Windows.Controls;
using Cmie.MotorTest.Wpf.Models;
using Cmie.MotorTest.Wpf.Services;

namespace Cmie.MotorTest.Wpf.Views;

public partial class NewTestPage : UserControl
{
    public NewTestPage()
    {
        InitializeComponent();
    }

    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    private void NewTestPage_Loaded(object sender, RoutedEventArgs e)
    {
        FillForm(DemoSession.CurrentProject ?? DemoSession.CreateDemoDefaults());
    }

    private void FillForm(TestProjectInfo info)
    {
        MotorIdBox.Text = info.MotorId;
        ModelBox.Text = info.Model;
        MakerBox.Text = info.Maker;
        FactoryNoBox.Text = info.FactoryNo;
        VoltageBox.Text = info.Voltage;
        CurrentBox.Text = info.Current;
        PowerBox.Text = info.Power;
        FreqBox.Text = info.Frequency;
        SpeedBox.Text = info.Speed;
        PfBox.Text = info.PowerFactor;
        ConnBox.Text = info.Connection;
        InsulBox.Text = info.Insulation;
        DutyBox.Text = info.Duty;
        IpBox.Text = info.IpRating;
        PolesBox.Text = info.Poles;
        CoolBox.Text = info.Cooling;
        SampleBox.Text = info.SampleName;
    }

    private TestProjectInfo ReadForm() => new()
    {
        MotorId = MotorIdBox.Text.Trim(),
        Model = ModelBox.Text.Trim(),
        Maker = MakerBox.Text.Trim(),
        FactoryNo = FactoryNoBox.Text.Trim(),
        Voltage = VoltageBox.Text.Trim(),
        Current = CurrentBox.Text.Trim(),
        Power = PowerBox.Text.Trim(),
        Frequency = FreqBox.Text.Trim(),
        Speed = SpeedBox.Text.Trim(),
        PowerFactor = PfBox.Text.Trim(),
        Connection = ConnBox.Text.Trim(),
        Insulation = InsulBox.Text.Trim(),
        Duty = DutyBox.Text.Trim(),
        IpRating = IpBox.Text.Trim(),
        Poles = PolesBox.Text.Trim(),
        Cooling = CoolBox.Text.Trim(),
        SampleName = SampleBox.Text.Trim()
    };

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Shell?.Navigate("home");
        Shell?.ShowToast("已取消新建试验");
    }

    private async void Enter_Click(object sender, RoutedEventArgs e)
    {
        var project = ReadForm();
        if (string.IsNullOrWhiteSpace(project.MotorId))
        {
            Shell?.ShowToast("请填写电机编号");
            MotorIdBox.Focus();
            return;
        }

        try
        {
            UserSession.EnsureCanModifyTests();
            await LocalDataService.Current.SaveProjectAsync(project);
            DemoSession.CurrentProject = project;
            Shell?.Navigate("project");
            Shell?.ShowToast($"项目已保存到本地：{project.MotorId}");
        }
        catch (Exception exception)
        {
            Shell?.ShowToast(LocalDataService.FriendlyError(exception));
        }
    }
}
