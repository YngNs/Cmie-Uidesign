using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Demo;

public static class DemoProjectData
{
    public static IReadOnlyList<TestItem> Tests { get; } =
    [
        new("resistance", "基础试验", "直流电阻和绝缘电阻测量"),
        new("noload", "基础试验", "空载特性试验"),
        new("load", "基础试验", "负载特性试验"),
        new("method-a", "效率分析", "A 法数据分析", true),
        new("method-b", "效率分析", "B 法数据分析", true),
        new("temp-rated", "温升试验", "额定工况", true),
        new("temp-low", "温升试验", "低频工况", true),
        new("locked", "其他", "堵转特性试验"),
        new("overspeed", "其他", "超速 / 振动噪声")
    ];

    public static List<MetricReading> CreateMetrics() =>
    [
        Metric("u1", "测量数据", "线电压1", "380.2", "V"),
        Metric("u2", "测量数据", "线电压2", "379.8", "V"),
        Metric("u3", "测量数据", "线电压3", "380.1", "V"),
        Metric("uavg", "测量数据", "平均电压", "380.0", "V", true),
        Metric("i1", "测量数据", "电流1", "12.41", "A"),
        Metric("i2", "测量数据", "电流2", "12.48", "A"),
        Metric("i3", "测量数据", "电流3", "12.46", "A"),
        Metric("iavg", "测量数据", "平均电流", "12.45", "A", true),
        Metric("p", "测量数据", "有功功率", "7820", "W"),
        Metric("q", "测量数据", "无功功率", "4.61", "kVar"),
        Metric("f", "测量数据", "频率", "50.01", "Hz"),
        Metric("pf", "测量数据", "功率因数", "0.86", ""),
        Metric("shaft", "轴功率", "轴功率", "7150", "W", true),
        Metric("n", "轴功率", "转速", "1486", "rpm", true),
        Metric("tq", "轴功率", "扭矩", "45.9", "Nm"),
        Metric("w1", "温度 · 被试设备", "绕组1", "62.4", "°C", true),
        Metric("w2", "温度 · 被试设备", "绕组2", "61.8", "°C"),
        Metric("w3", "温度 · 被试设备", "绕组3", "62.1", "°C"),
        Metric("de1", "温度 · 被试设备", "DE1 轴承", "48.2", "°C"),
        Metric("nde1", "温度 · 被试设备", "NDE1 轴承", "46.7", "°C"),
        Metric("amb", "温度 · 被试设备", "环境温度", "26.4", "°C")
    ];

    private static MetricReading Metric(
        string id,
        string group,
        string label,
        string value,
        string unit,
        bool pinned = false) => new()
        {
            Id = id,
            Group = group,
            Label = label,
            Value = value,
            Unit = unit,
            IsPinned = pinned
        };
}
