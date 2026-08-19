using System.Globalization;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

public static class NoLoadCalculationService
{
    private const int ColumnCount = 12;

    public static NoLoadCalculationResult Calculate(
        WorksheetDocument worksheet,
        double ratedVoltage,
        double statorLineResistance)
    {
        if (worksheet.TestKey != "noload") throw new ArgumentException("当前工作表不是空载试验。", nameof(worksheet));
        if (ratedVoltage <= 0) throw new ArgumentOutOfRangeException(nameof(ratedVoltage), "额定电压必须大于 0。 ");
        if (statorLineResistance <= 0) throw new ArgumentOutOfRangeException(nameof(statorLineResistance), "定子线电阻必须大于 0。 ");

        var points = new List<NoLoadPointResult>();
        for (var row = 0; row * ColumnCount < worksheet.Values.Count; row++)
        {
            var offset = row * ColumnCount;
            var voltage = Read(worksheet.Values, offset + 3) ?? AverageAvailable(worksheet.Values, offset, 3);
            var current = Read(worksheet.Values, offset + 7) ?? AverageAvailable(worksheet.Values, offset + 4, 3);
            var power = Read(worksheet.Values, offset + 8);
            if (voltage is null || current is null || power is null) continue;
            if (voltage <= 0 || current < 0 || power < 0) continue;

            var apparentPower = Math.Sqrt(3) * voltage.Value * current.Value;
            var powerFactor = apparentPower <= double.Epsilon ? 0 : power.Value / apparentPower;
            if (powerFactor > 1.02) throw new ArgumentException($"第 {row + 1} 行功率因数超过 1，请检查电压、电流或功率单位。 ");
            var copperLoss = 1.5 * current.Value * current.Value * statorLineResistance;
            points.Add(new NoLoadPointResult(
                row, voltage.Value, current.Value, power.Value, Math.Clamp(powerFactor, 0, 1),
                copperLoss, power.Value - copperLoss));
        }

        if (points.Count < 4) throw new ArgumentException("空载计算至少需要 4 个完整测量点（电压、电流、功率）。");
        var x = points.Select(point => point.Voltage / ratedVoltage * 100).ToArray();
        var powerFit = PolynomialFit(x, points.Select(point => point.InputPower).ToArray(), 2);
        var currentFit = PolynomialFit(x, points.Select(point => point.Current).ToArray(), 2);
        var constantFit = PolynomialFit(x, points.Select(point => point.ConstantLoss).ToArray(), 2);

        var lowVoltagePoints = points
            .Select((point, index) => (Point: point, Percent: x[index]))
            .Where(item => item.Percent < 60)
            .ToArray();
        if (lowVoltagePoints.Length < 2)
            throw new ArgumentException("机械损耗拟合至少需要 2 个低于 60% 额定电压的测量点。 ");
        var mechanicalFit = PolynomialFit(
            lowVoltagePoints.Select(item => item.Percent * item.Percent / 100).ToArray(),
            lowVoltagePoints.Select(item => item.Point.ConstantLoss).ToArray(), 1);

        var ratedInputPower = Evaluate(powerFit, 100);
        var ratedCurrent = Evaluate(currentFit, 100);
        var ratedConstantLoss = Evaluate(constantFit, 100);
        var mechanicalLoss = Math.Max(0, mechanicalFit[0]);
        var ironLoss = Math.Max(0, ratedConstantLoss - mechanicalLoss);
        var ironLossFit = constantFit.ToArray();
        ironLossFit[0] -= mechanicalLoss;
        var ratedPowerFactor = ratedInputPower / (Math.Sqrt(3) * ratedVoltage * ratedCurrent);

        return new NoLoadCalculationResult(
            points,
            ratedCurrent,
            ratedInputPower,
            Math.Clamp(ratedPowerFactor, 0, 1),
            mechanicalLoss,
            ironLoss,
            ironLossFit,
            powerFit,
            currentFit);
    }

    private static double? AverageAvailable(IReadOnlyList<string> values, int offset, int count)
    {
        var available = Enumerable.Range(offset, count).Select(index => Read(values, index)).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return available.Length == count ? available.Average() : null;
    }

    private static double? Read(IReadOnlyList<string> values, int index)
    {
        if (index >= values.Count || string.IsNullOrWhiteSpace(values[index])) return null;
        return double.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : double.TryParse(values[index], NumberStyles.Float, CultureInfo.CurrentCulture, out var local) ? local : null;
    }

    private static double[] PolynomialFit(double[] x, double[] y, int degree)
    {
        if (x.Length != y.Length || x.Length <= degree) throw new ArgumentException("拟合数据点不足。 ");
        var size = degree + 1;
        var matrix = new double[size, size + 1];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++) matrix[row, column] = x.Sum(value => Math.Pow(value, row + column));
            matrix[row, size] = x.Zip(y, (value, output) => Math.Pow(value, row) * output).Sum();
        }
        for (var pivot = 0; pivot < size; pivot++)
        {
            var best = Enumerable.Range(pivot, size - pivot).MaxBy(row => Math.Abs(matrix[row, pivot]));
            if (Math.Abs(matrix[best, pivot]) < 1e-12) throw new ArgumentException("测量点无法形成稳定拟合，请检查是否存在重复电压。 ");
            if (best != pivot) for (var column = pivot; column <= size; column++) (matrix[pivot, column], matrix[best, column]) = (matrix[best, column], matrix[pivot, column]);
            var divisor = matrix[pivot, pivot];
            for (var column = pivot; column <= size; column++) matrix[pivot, column] /= divisor;
            for (var row = 0; row < size; row++)
            {
                if (row == pivot) continue;
                var factor = matrix[row, pivot];
                for (var column = pivot; column <= size; column++) matrix[row, column] -= factor * matrix[pivot, column];
            }
        }
        return Enumerable.Range(0, size).Select(row => matrix[row, size]).ToArray();
    }

    private static double Evaluate(IReadOnlyList<double> coefficients, double x) =>
        coefficients.Select((coefficient, power) => coefficient * Math.Pow(x, power)).Sum();
}
