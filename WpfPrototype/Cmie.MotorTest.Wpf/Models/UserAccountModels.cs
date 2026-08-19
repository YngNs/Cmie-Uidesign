namespace Cmie.MotorTest.Wpf.Models;

public enum UserRole
{
    Administrator,
    Operator,
    ReadOnly
}

public sealed record UserAccountSummary(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string Note,
    bool IsProtected)
{
    public string Initial => string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName[..1].ToUpperInvariant();
    public string AccountMeta => $"@{Username}  ·  创建于 {CreatedAt.ToLocalTime():yyyy-MM-dd}";
    public string RoleDisplay => Role switch
    {
        UserRole.Administrator => "管理员",
        UserRole.Operator => "试验员",
        _ => "只读用户"
    };

    public string StatusDisplay => IsEnabled ? "启用" : "停用";
    public string LastLoginDisplay => LastLoginAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "从未登录";
}

public sealed record AuthenticatedUser(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role);

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string Password,
    UserRole Role,
    bool IsEnabled,
    string Note);

public sealed record UpdateUserRequest(
    Guid Id,
    string DisplayName,
    UserRole Role,
    bool IsEnabled,
    string Note,
    string? NewPassword);

internal sealed record StoredUserAccount(
    Guid Id,
    string Username,
    string DisplayName,
    string PasswordHash,
    string PasswordSalt,
    int PasswordIterations,
    UserRole Role,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string Note,
    bool IsProtected,
    int FailedLoginAttempts,
    DateTimeOffset? LockedUntil,
    string? PasswordCiphertext = null);

internal sealed record UserStoreDocument(int SchemaVersion, IReadOnlyList<StoredUserAccount> Users);
