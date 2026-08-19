using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

/// <summary>
/// Three-phase winding resistance calculations migrated from the legacy VB ClassRDeal.
/// </summary>
public static class ResistanceCalculationService
{
    public static ResistanceCalculationResult Calculate(ResistanceCalculationInput input)
    {
        Validate(input);

        var measuredPhase = LineToPhase(
            input.LineAb,
            input.LineBc,
            input.LineCa,
            input.Connection);
        var temperatureFactor =
            (input.TemperatureConstant + input.ReferenceTemperature)
            / (input.TemperatureConstant + input.MeasuredTemperature);

        var lineAtReference = new ThreePhaseResistance(
            input.LineAb * temperatureFactor,
            input.LineBc * temperatureFactor,
            input.LineCa * temperatureFactor);
        var phaseAtReference = new ThreePhaseResistance(
            measuredPhase.PhaseA * temperatureFactor,
            measuredPhase.PhaseB * temperatureFactor,
            measuredPhase.PhaseC * temperatureFactor);
        var average = Average(phaseAtReference);
        var maximumDeviation = new[]
        {
            Math.Abs(phaseAtReference.PhaseA - average),
            Math.Abs(phaseAtReference.PhaseB - average),
            Math.Abs(phaseAtReference.PhaseC - average)
        }.Max();

        return new ResistanceCalculationResult(
            lineAtReference,
            phaseAtReference,
            average,
            maximumDeviation / average * 100.0);
    }

    public static double TemperatureFromResistance(
        double coldResistance,
        double coldTemperature,
        double hotResistance,
        double temperatureConstant = 235.0)
    {
        if (coldResistance <= 0 || hotResistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coldResistance), "电阻值必须大于 0。");
        }

        return (hotResistance - coldResistance)
            * (temperatureConstant + coldTemperature)
            / coldResistance
            + coldTemperature;
    }

    public static double ResistanceAtTemperature(
        double resistance,
        double measuredTemperature,
        double targetTemperature,
        double temperatureConstant = 235.0)
    {
        if (resistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resistance), "电阻值必须大于 0。");
        }

        if (Math.Abs(temperatureConstant + measuredTemperature) < double.Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredTemperature), "测量温度导致折算分母为 0。");
        }

        return resistance
            * (temperatureConstant + targetTemperature)
            / (temperatureConstant + measuredTemperature);
    }

    private static ThreePhaseResistance LineToPhase(
        double lineAb,
        double lineBc,
        double lineCa,
        WindingConnection connection)
    {
        var semiPerimeter = (lineAb + lineBc + lineCa) / 2.0;
        if (connection == WindingConnection.Star)
        {
            return new ThreePhaseResistance(
                semiPerimeter - lineBc,
                semiPerimeter - lineCa,
                semiPerimeter - lineAb);
        }

        var denominatorA = semiPerimeter - lineAb;
        var denominatorB = semiPerimeter - lineBc;
        var denominatorC = semiPerimeter - lineCa;
        if (denominatorA <= 0 || denominatorB <= 0 || denominatorC <= 0)
        {
            throw new ArgumentException("三角形接法的线电阻组合无法换算为有效相电阻。");
        }

        return new ThreePhaseResistance(
            lineBc * lineCa / denominatorA + lineAb - semiPerimeter,
            lineCa * lineAb / denominatorB + lineBc - semiPerimeter,
            lineBc * lineAb / denominatorC + lineCa - semiPerimeter);
    }

    private static double Average(ThreePhaseResistance resistance) =>
        (resistance.PhaseA + resistance.PhaseB + resistance.PhaseC) / 3.0;

    private static void Validate(ResistanceCalculationInput input)
    {
        if (input.LineAb <= 0 || input.LineBc <= 0 || input.LineCa <= 0)
        {
            throw new ArgumentException("三路线电阻必须全部填写并且大于 0。");
        }

        if (input.TemperatureConstant <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input.TemperatureConstant), "温度常数必须大于 0。");
        }

        if (Math.Abs(input.TemperatureConstant + input.MeasuredTemperature) < double.Epsilon)
        {
            throw new ArgumentException("测量温度导致折算分母为 0。");
        }
    }
}
