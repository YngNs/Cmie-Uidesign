namespace Cmie.MotorTest.Wpf.Models;

public sealed class TestProjectInfo
{
    public string MotorId { get; set; } = "";
    public string Model { get; set; } = "";
    public string Maker { get; set; } = "";
    public string FactoryNo { get; set; } = "";
    public string Voltage { get; set; } = "";
    public string Current { get; set; } = "";
    public string Power { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Speed { get; set; } = "";
    public string PowerFactor { get; set; } = "";
    public string Connection { get; set; } = "";
    public string Insulation { get; set; } = "";
    public string Duty { get; set; } = "";
    public string IpRating { get; set; } = "";
    public string Poles { get; set; } = "";
    public string Cooling { get; set; } = "";
    public string SampleName { get; set; } = "";
}

public static class DemoSession
{
    public static TestProjectInfo? CurrentProject { get; set; }
    public static string MotorType { get; set; } = "工频电机";

    public static TestProjectInfo CreateDemoDefaults() => new()
    {
        MotorId = "2025DJXXXX",
        Model = "Y2-160M-4",
        Maker = "中机国际演示",
        FactoryNo = "FN-2025-0418",
        Voltage = "380",
        Current = "15.0",
        Power = "11",
        Frequency = "50",
        Speed = "1460",
        PowerFactor = "0.85",
        Connection = "Y",
        Insulation = "F",
        Duty = "S1",
        IpRating = "IP54",
        Poles = "4",
        Cooling = "IC411",
        SampleName = "三相异步电机样机"
    };
}
