namespace Cmie.MotorTest.Wpf.Models;

public sealed record MethodAPointResult(
    int PointIndex,
    double WindingTemperature,
    double Voltage,
    double Current,
    double InputPower,
    double Frequency,
    double SynchronousSpeed,
    double Speed,
    double Slip,
    double SlipPercent,
    double CorrectedSlip,
    double CorrectedSpeed,
    double Torque,
    double CorrectedTorque,
    double CorrectedOutputPower,
    double IronLoss,
    double StatorCopperLoss,
    double CorrectedStatorCopperLoss,
    double StatorCopperLossDelta,
    double RotorCopperLoss,
    double CorrectedRotorCopperLoss,
    double RotorCopperLossDelta,
    double CorrectedInputPower,
    double MechanicalLoss,
    double StrayLoss,
    double Efficiency,
    double PowerFactor);

public sealed record MethodACalculationResult(
    IReadOnlyList<MethodAPointResult> Points,
    MethodAPointResult RatedPoint);
