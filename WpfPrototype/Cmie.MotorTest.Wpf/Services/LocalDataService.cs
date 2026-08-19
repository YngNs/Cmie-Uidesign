using System.Globalization;
using System.IO;
using System.Text.Json;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

public sealed class LocalDataService
{
    private static readonly Lazy<LocalDataService> LazyInstance = new(() => new LocalDataService());
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LocalDataService()
    {
        DataDirectory = Environment.GetEnvironmentVariable("CMIE_DATA_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CMIE",
                "MotorTest",
                "Data");
        Directory.CreateDirectory(DataDirectory);
    }

    public static LocalDataService Current => LazyInstance.Value;

    public string DataDirectory { get; }

    public Task<LocalServiceStatus> GetStatusAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new LocalServiceStatus(DataDirectory, DateTimeOffset.Now));
    }

    public Task SaveProjectAsync(TestProjectInfo project, CancellationToken token = default) =>
        WriteAsync(ProjectPath(project.MotorId), project, token);

    public Task SaveWorksheetAsync(WorksheetDocument worksheet, CancellationToken token = default)
    {
        var saved = worksheet with { UpdatedAt = DateTimeOffset.Now };
        return WriteAsync(WorksheetPath(saved.ProjectId, saved.TestKey), saved, token);
    }

    public async Task<WorksheetRecordSummary> SaveWorksheetRecordAsync(
        WorksheetDocument worksheet,
        string? name = null,
        CancellationToken token = default)
    {
        var now = DateTimeOffset.Now;
        var saved = worksheet with { UpdatedAt = now };
        var record = new WorksheetRecordDocument(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(name) ? $"{worksheet.TestTitle} {now:yyyy-MM-dd HH:mm:ss}" : name.Trim(),
            UserSession.Current?.DisplayName ?? "未知用户",
            now,
            now,
            saved);
        await WriteAsync(WorksheetRecordPath(saved.ProjectId, saved.TestKey, record.RecordId), record, token);
        await WriteAsync(WorksheetPath(saved.ProjectId, saved.TestKey), saved, token);
        return ToSummary(record);
    }

    public async Task<IReadOnlyList<WorksheetRecordSummary>> ListWorksheetRecordsAsync(
        string projectId,
        string testKey,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var directory = WorksheetRecordsDirectory(projectId, testKey);
            if (!Directory.Exists(directory)) return [];
            var records = new List<WorksheetRecordSummary>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                token.ThrowIfCancellationRequested();
                await using var stream = File.OpenRead(path);
                var record = await JsonSerializer.DeserializeAsync<WorksheetRecordDocument>(stream, _json, token);
                if (record is not null) records.Add(ToSummary(record));
            }
            return records.OrderByDescending(record => record.UpdatedAt).ToArray();
        }
        finally { _gate.Release(); }
    }

    public Task<WorksheetRecordDocument?> LoadWorksheetRecordAsync(
        string projectId,
        string testKey,
        Guid recordId,
        CancellationToken token = default) =>
        ReadAsync<WorksheetRecordDocument>(WorksheetRecordPath(projectId, testKey, recordId), token);

    public async Task DeleteWorksheetRecordAsync(
        string projectId,
        string testKey,
        Guid recordId,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var path = WorksheetRecordPath(projectId, testKey, recordId);
            if (File.Exists(path)) File.Delete(path);
        }
        finally { _gate.Release(); }
    }

    public Task<WorksheetDocument?> LoadWorksheetAsync(
        string projectId,
        string testKey,
        CancellationToken token = default) =>
        ReadAsync<WorksheetDocument>(WorksheetPath(projectId, testKey), token);

    public async Task DeleteWorksheetAsync(
        string projectId,
        string testKey,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var path = WorksheetPath(projectId, testKey);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<CalculationResult> CalculateAsync(
        WorksheetDocument worksheet,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var numbers = worksheet.Values
            .Select(ParseNumber)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        var result = numbers.Length == 0
            ? new CalculationResult(0, null, null, null, null)
            : new CalculationResult(
                numbers.Length,
                numbers.Average(),
                numbers.Min(),
                numbers.Max(),
                Math.Abs(numbers.Max()) < double.Epsilon
                    ? 0
                    : (numbers.Max() - numbers.Min()) / Math.Abs(numbers.Max()) * 100);

        return Task.FromResult(result);
    }

    public Task SaveNoLoadAnalysisAsync(string projectId, NoLoadCalculationResult result, CancellationToken token = default) =>
        WriteAsync(NoLoadAnalysisPath(projectId), result, token);

    public Task<NoLoadCalculationResult?> LoadNoLoadAnalysisAsync(string projectId, CancellationToken token = default) =>
        ReadAsync<NoLoadCalculationResult>(NoLoadAnalysisPath(projectId), token);

    public static string FriendlyError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "没有权限访问本地数据目录。",
        IOException => $"本地数据读写失败：{exception.Message}",
        JsonException => "本地数据文件格式无效。",
        TaskCanceledException => "本地数据操作已取消。",
        ArgumentException => exception.Message,
        InvalidOperationException => exception.Message,
        _ => $"本地数据操作失败：{exception.Message}"
    };

    private async Task<T?> ReadAsync<T>(string path, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, _json, token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = path + ".tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, _json, token);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ProjectPath(string projectId) =>
        Path.Combine(DataDirectory, "projects", Safe(projectId) + ".json");

    private string WorksheetPath(string projectId, string testKey) =>
        Path.Combine(DataDirectory, "worksheets", Safe(projectId), Safe(testKey) + ".json");

    private string WorksheetRecordsDirectory(string projectId, string testKey) =>
        Path.Combine(DataDirectory, "worksheet-records", Safe(projectId), Safe(testKey));

    private string WorksheetRecordPath(string projectId, string testKey, Guid recordId) =>
        Path.Combine(WorksheetRecordsDirectory(projectId, testKey), recordId.ToString("N") + ".json");

    private string NoLoadAnalysisPath(string projectId) =>
        Path.Combine(DataDirectory, "analysis-results", Safe(projectId), "noload.json");

    private static WorksheetRecordSummary ToSummary(WorksheetRecordDocument record) => new(
        record.RecordId, record.Name, record.SavedBy, record.CreatedAt, record.UpdatedAt);

    private static double? ParseNumber(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var local)
            ? local
            : null;
    }

    private static string Safe(string value)
    {
        var cleaned = string.Concat(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
    }
}
