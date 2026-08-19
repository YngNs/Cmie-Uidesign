using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("CMIE_API_URL") ?? "http://127.0.0.1:5188");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<JsonDataStore>();

var app = builder.Build();

app.MapGet("/api/health", (JsonDataStore store) => Results.Ok(new
{
    status = "ok",
    service = "CMIE MotorTest API",
    dataDirectory = store.DataDirectory,
    serverTime = DateTimeOffset.Now
}));

app.MapGet("/api/projects/{projectId}", async (string projectId, JsonDataStore store, CancellationToken token) =>
{
    var project = await store.ReadProjectAsync(projectId, token);
    return project is null ? Results.NotFound() : Results.Ok(project);
});

app.MapPut("/api/projects/{projectId}", async (string projectId, ProjectDocument project, JsonDataStore store, CancellationToken token) =>
{
    if (!string.Equals(projectId, project.MotorId, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "路径项目编号与数据中的 motorId 不一致。" });
    await store.WriteProjectAsync(project, token);
    return Results.Ok(project);
});

app.MapGet("/api/projects/{projectId}/worksheets/{testKey}", async (string projectId, string testKey, JsonDataStore store, CancellationToken token) =>
{
    var worksheet = await store.ReadWorksheetAsync(projectId, testKey, token);
    return worksheet is null ? Results.NotFound() : Results.Ok(worksheet);
});

app.MapPut("/api/projects/{projectId}/worksheets/{testKey}", async (
    string projectId, string testKey, WorksheetDocument worksheet, JsonDataStore store, CancellationToken token) =>
{
    if (!string.Equals(projectId, worksheet.ProjectId, StringComparison.OrdinalIgnoreCase)
        || !string.Equals(testKey, worksheet.TestKey, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "路径与工作表标识不一致。" });

    var saved = worksheet with { UpdatedAt = DateTimeOffset.Now };
    await store.WriteWorksheetAsync(saved, token);
    return Results.Ok(saved);
});

app.MapDelete("/api/projects/{projectId}/worksheets/{testKey}", async (string projectId, string testKey, JsonDataStore store, CancellationToken token) =>
{
    await store.DeleteWorksheetAsync(projectId, testKey, token);
    return Results.NoContent();
});

app.MapPost("/api/calculate", (WorksheetDocument worksheet) =>
{
    var numbers = worksheet.Values
        .Select(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)
            ? invariant
            : double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var local) ? local : (double?)null)
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
            Math.Abs(numbers.Max()) < double.Epsilon ? 0 : (numbers.Max() - numbers.Min()) / Math.Abs(numbers.Max()) * 100);
    return Results.Ok(result);
});

app.Run();

public sealed record ProjectDocument(
    string MotorId,
    string Model,
    string Maker,
    string FactoryNo,
    string Voltage,
    string Current,
    string Power,
    string Frequency,
    string Speed,
    string PowerFactor,
    string Connection,
    string Insulation,
    string Duty,
    string IpRating,
    string Poles,
    string Cooling,
    string SampleName);

public sealed record WorksheetDocument(
    string ProjectId,
    string TestKey,
    string TestTitle,
    IReadOnlyList<string> Values,
    DateTimeOffset UpdatedAt);

public sealed record CalculationResult(int NumericCount, double? Average, double? Minimum, double? Maximum, double? UnbalancePercent);

public sealed class JsonDataStore
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonDataStore()
    {
        DataDirectory = Environment.GetEnvironmentVariable("CMIE_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMIE", "MotorTest", "Data");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }

    public Task<ProjectDocument?> ReadProjectAsync(string projectId, CancellationToken token) =>
        ReadAsync<ProjectDocument>(ProjectPath(projectId), token);

    public Task WriteProjectAsync(ProjectDocument project, CancellationToken token) =>
        WriteAsync(ProjectPath(project.MotorId), project, token);

    public Task<WorksheetDocument?> ReadWorksheetAsync(string projectId, string testKey, CancellationToken token) =>
        ReadAsync<WorksheetDocument>(WorksheetPath(projectId, testKey), token);

    public Task WriteWorksheetAsync(WorksheetDocument worksheet, CancellationToken token) =>
        WriteAsync(WorksheetPath(worksheet.ProjectId, worksheet.TestKey), worksheet, token);

    public async Task DeleteWorksheetAsync(string projectId, string testKey, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var path = WorksheetPath(projectId, testKey);
            if (File.Exists(path)) File.Delete(path);
        }
        finally { _gate.Release(); }
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (!File.Exists(path)) return default;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, _json, token);
        }
        finally { _gate.Release(); }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            await using (var stream = File.Create(temp))
                await JsonSerializer.SerializeAsync(stream, value, _json, token);
            File.Move(temp, path, true);
        }
        finally { _gate.Release(); }
    }

    private string ProjectPath(string projectId) => Path.Combine(DataDirectory, "projects", Safe(projectId) + ".json");
    private string WorksheetPath(string projectId, string testKey) =>
        Path.Combine(DataDirectory, "worksheets", Safe(projectId), Safe(testKey) + ".json");

    private static string Safe(string value)
    {
        var cleaned = string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        return string.IsNullOrWhiteSpace(cleaned) ? "unnamed" : cleaned;
    }
}
