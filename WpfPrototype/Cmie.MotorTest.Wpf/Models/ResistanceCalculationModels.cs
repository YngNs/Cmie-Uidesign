namespace Cmie.MotorTest.Wpf.Models;

public enum WindingConnection
{
    Star,
    Delta
}

public sealed record ResistanceCalculationInput(
    double LineAb,
    double LineBc,
    double LineCa,
    double MeasuredTemperature,
    double ReferenceTemperature,
    WindingConnection Connection,
    double TemperatureConstant = 235.0);

public sealed record ThreePhaseResistance(double PhaseA, double PhaseB, double PhaseC);

public sealed record ResistanceCalculationResult(
    ThreePhaseResistance LineAtReference,
    ThreePhaseResistance PhaseAtReference,
    double AveragePhaseResistance,
    double UnbalancePercent);
