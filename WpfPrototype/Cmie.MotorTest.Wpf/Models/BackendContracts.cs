namespace Cmie.MotorTest.Wpf.Models;

public sealed record WorksheetDocument(
    string ProjectId,
    string TestKey,
    string TestTitle,
    IReadOnlyList<string> Values,
    DateTimeOffset UpdatedAt,
    IReadOnlyDictionary<string, string>? Fields = null);

public sealed record WorksheetRecordSummary(
    Guid RecordId,
    string Name,
    string SavedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorksheetRecordDocument(
    Guid RecordId,
    string Name,
    string SavedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    WorksheetDocument Worksheet);

public sealed record CalculationResult(
    int NumericCount,
    double? Average,
    double? Minimum,
    double? Maximum,
    double? UnbalancePercent);

public sealed record LocalServiceStatus(string DataDirectory, DateTimeOffset CheckedAt);
