namespace Cmie.MotorTest.Wpf.Models;

public sealed record NoLoadPointResult(
    int RowIndex,
    double Voltage,
    double Current,
    double InputPower,
    double PowerFactor,
    double StatorCopperLoss,
    double ConstantLoss);

public sealed record NoLoadCalculationResult(
    IReadOnlyList<NoLoadPointResult> Points,
    double RatedCurrent,
    double RatedInputPower,
    double RatedPowerFactor,
    double MechanicalLoss,
    double IronLoss,
    IReadOnlyList<double> IronLossCoefficients,
    IReadOnlyList<double> InputPowerCoefficients,
    IReadOnlyList<double> CurrentCoefficients);
