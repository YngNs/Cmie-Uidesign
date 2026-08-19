using System.Globalization;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

public static class MethodACalculationService
{
    private const int PointCount = 12;

    public static MethodACalculationResult Calculate(
        WorksheetDocument worksheet,
        NoLoadCalculationResult noLoad,
        double ratedVoltage,
        double ratedFrequency,
        int poles,
        double ratedPowerWatts,
        double initialLineResistance,
        double initialTemperature,
        double torqueCompensation = 0)
    {
        if (worksheet.TestKey != "method-a") throw new ArgumentException("当前工作表不是 A 法分析。 ");
        if (ratedVoltage <= 0 || ratedFrequency <= 0 || poles <= 0 || poles % 2 != 0) throw new ArgumentException("额定电压、频率或极数无效。 ");
        var results = new List<MethodAPointResult>();
        for (var point = 0; point < PointCount; point++)
        {
            var temperature = Read(worksheet.Values, 0, point);
            var voltage = Read(worksheet.Values, 1, point);
            var current = Read(worksheet.Values, 2, point);
            var inputPower = Read(worksheet.Values, 3, point);
            var frequency = Read(worksheet.Values, 4, point);
            var speed = Read(worksheet.Values, 6, point);
            var torque = Read(worksheet.Values, 11, point);
            if (temperature is null || voltage is null || current is null || inputPower is null || frequency is null || speed is null || torque is null) continue;
            if (voltage <= 0 || current <= 0 || inputPower <= 0 || frequency <= 0 || speed <= 0) continue;

            var synchronousSpeed = 120 * frequency.Value / poles;
            var slip = synchronousSpeed - speed.Value;
            if (slip < 0) throw new ArgumentException($"第 {point + 1} 点转速高于同步转速，请检查数据。 ");
            var slipPercent = slip / synchronousSpeed * 100;
            var correctedSlip = slip * 260 / (235 + temperature.Value);
            var correctedSpeed = synchronousSpeed - correctedSlip;
            var correctedTorque = torque.Value + Math.Abs(torqueCompensation);
            var correctedOutput = correctedTorque * correctedSpeed * 2 * Math.PI / 60;

            var measuredPowerFactor = inputPower.Value / (Math.Sqrt(3) * voltage.Value * current.Value);
            if (measuredPowerFactor >= 1.02) throw new ArgumentException($"第 {point + 1} 点功率因数超过 1，请检查功率单位。 ");
            var sin = Math.Sqrt(Math.Max(0, 1 - measuredPowerFactor * measuredPowerFactor));
            var resistanceAtTemperature = initialLineResistance * (235 + temperature.Value) / (235 + initialTemperature);
            var correctedVoltage = Math.Sqrt(
                Math.Pow(voltage.Value - 0.866 * current.Value * resistanceAtTemperature * measuredPowerFactor, 2)
                + Math.Pow(0.866 * current.Value * resistanceAtTemperature * sin, 2));
            var ironLoss = Math.Max(0, Evaluate(noLoad.IronLossCoefficients, correctedVoltage / ratedVoltage * 100));
            var statorLoss = 1.5 * current.Value * current.Value * resistanceAtTemperature;
            var correctedStatorLoss = statorLoss * 260 / (235 + temperature.Value);
            var statorDelta = statorLoss - correctedStatorLoss;
            var rotorLoss = (inputPower.Value - statorLoss - ironLoss) * slipPercent / 100;
            var correctedRotorLoss = rotorLoss * 260 / (235 + temperature.Value);
            var rotorDelta = rotorLoss - correctedRotorLoss;
            var correctedInput = inputPower.Value + statorDelta + rotorDelta;
            var efficiency = correctedOutput / correctedInput * 100;
            var strayLoss = correctedInput - correctedOutput - noLoad.MechanicalLoss - correctedStatorLoss - correctedRotorLoss - ironLoss;
            var powerFactor = correctedInput / (Math.Sqrt(3) * voltage.Value * current.Value);
            results.Add(new MethodAPointResult(
                point, temperature.Value, voltage.Value, current.Value, inputPower.Value, frequency.Value,
                synchronousSpeed, speed.Value, slip, slipPercent, correctedSlip, correctedSpeed,
                torque.Value, correctedTorque, correctedOutput, ironLoss, statorLoss, correctedStatorLoss,
                statorDelta, rotorLoss, correctedRotorLoss, rotorDelta, correctedInput, noLoad.MechanicalLoss,
                strayLoss, efficiency, powerFactor));
        }

        if (results.Count < 3) throw new ArgumentException("A 法分析至少需要 3 个完整负载点（温度、电压、电流、功率、频率、转速、转矩）。");
        var rated = results.MinBy(point => Math.Abs(point.CorrectedOutputPower - ratedPowerWatts))!;
        return new MethodACalculationResult(results, rated);
    }

    private static double? Read(IReadOnlyList<string> values, int row, int point)
    {
        var index = row * PointCount + point;
        if (index >= values.Count || string.IsNullOrWhiteSpace(values[index])) return null;
        return double.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : double.TryParse(values[index], NumberStyles.Float, CultureInfo.CurrentCulture, out var local) ? local : null;
    }

    private static double Evaluate(IReadOnlyList<double> coefficients, double x) =>
        coefficients.Select((coefficient, power) => coefficient * Math.Pow(x, power)).Sum();
}
