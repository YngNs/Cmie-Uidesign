using System.IO;
using System.Text.Json;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

public sealed class UserAccountService
{
    public const string BootstrapUsername = "admin";
    public const string BootstrapPassword = "Admin@123";
    private static readonly Lazy<UserAccountService> LazyInstance = new(() => new UserAccountService());
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _storePath;

    private UserAccountService()
    {
        _storePath = Path.Combine(LocalDataService.Current.DataDirectory, "security", "users.json");
    }

    public static UserAccountService Current => LazyInstance.Value;

    public async Task<IReadOnlyList<UserAccountSummary>> ListAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            return document.Users
                .OrderByDescending(user => user.Role == UserRole.Administrator)
                .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                .Select(ToSummary)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AuthenticatedUser> AuthenticateAsync(
        string username,
        string password,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            var users = document.Users.ToList();
            var index = users.FindIndex(user =>
                user.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException("用户名或密码错误。");
            }

            var account = users[index];
            if (!account.IsEnabled)
            {
                throw new InvalidOperationException("该用户已停用，请联系管理员。");
            }

            var now = DateTimeOffset.Now;
            if (!PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt, account.PasswordIterations))
            {
                throw new InvalidOperationException("用户名或密码错误。");
            }

            account = account with
            {
                LastLoginAt = now,
                FailedLoginAttempts = 0,
                LockedUntil = null,
                PasswordCiphertext = account.PasswordCiphertext ?? WindowsPasswordProtector.Protect(password)
            };
            users[index] = account;
            await SaveUnsafeAsync(new UserStoreDocument(document.SchemaVersion, users), token);
            return new AuthenticatedUser(account.Id, account.Username, account.DisplayName, account.Role);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccountSummary> CreateAsync(
        CreateUserRequest request,
        CancellationToken token = default)
    {
        ValidateUsername(request.Username);
        ValidateDisplayName(request.DisplayName);
        ValidatePassword(request.Password);

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            if (document.Users.Any(user =>
                user.Username.Equals(request.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("用户名已存在。");
            }

            var password = PasswordHasher.Hash(request.Password);
            var account = new StoredUserAccount(
                Guid.NewGuid(),
                request.Username.Trim(),
                request.DisplayName.Trim(),
                password.Hash,
                password.Salt,
                password.Iterations,
                request.Role,
                request.IsEnabled,
                DateTimeOffset.Now,
                null,
                request.Note.Trim(),
                false,
                0,
                null,
                WindowsPasswordProtector.Protect(request.Password));
            var users = document.Users.Append(account).ToArray();
            await SaveUnsafeAsync(new UserStoreDocument(document.SchemaVersion, users), token);
            return ToSummary(account);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UserAccountSummary> UpdateAsync(
        UpdateUserRequest request,
        CancellationToken token = default)
    {
        ValidateDisplayName(request.DisplayName);
        if (!string.IsNullOrWhiteSpace(request.NewPassword)) ValidatePassword(request.NewPassword);

        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            var users = document.Users.ToList();
            var index = users.FindIndex(user => user.Id == request.Id);
            if (index < 0) throw new InvalidOperationException("用户不存在或已被删除。");

            var current = users[index];
            if (current.IsProtected && (request.Role != UserRole.Administrator || !request.IsEnabled))
            {
                throw new InvalidOperationException("系统保护管理员不能停用或更改角色。");
            }

            var updated = current with
            {
                DisplayName = request.DisplayName.Trim(),
                Role = request.Role,
                IsEnabled = request.IsEnabled,
                Note = request.Note.Trim()
            };
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var password = PasswordHasher.Hash(request.NewPassword);
                updated = updated with
                {
                    PasswordHash = password.Hash,
                    PasswordSalt = password.Salt,
                    PasswordIterations = password.Iterations,
                    FailedLoginAttempts = 0,
                    LockedUntil = null,
                    PasswordCiphertext = WindowsPasswordProtector.Protect(request.NewPassword)
                };
            }

            users[index] = updated;
            await SaveUnsafeAsync(new UserStoreDocument(document.SchemaVersion, users), token);
            return ToSummary(updated);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid userId, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            var account = document.Users.FirstOrDefault(user => user.Id == userId)
                ?? throw new InvalidOperationException("用户不存在或已被删除。");
            if (account.IsProtected) throw new InvalidOperationException("系统保护用户不能删除。");
            if (UserSession.Current?.Id == userId) throw new InvalidOperationException("不能删除当前登录用户。");

            var users = document.Users.Where(user => user.Id != userId).ToArray();
            if (!users.Any(user => user.Role == UserRole.Administrator && user.IsEnabled))
            {
                throw new InvalidOperationException("系统必须至少保留一个启用的管理员。");
            }

            await SaveUnsafeAsync(new UserStoreDocument(document.SchemaVersion, users), token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> RevealPasswordAsync(Guid userId, CancellationToken token = default)
    {
        UserSession.EnsureAdministrator();
        await _gate.WaitAsync(token);
        try
        {
            var document = await LoadOrCreateUnsafeAsync(token);
            var account = document.Users.FirstOrDefault(user => user.Id == userId)
                ?? throw new InvalidOperationException("用户不存在或已被删除。");
            if (string.IsNullOrWhiteSpace(account.PasswordCiphertext)) return null;
            try
            {
                return WindowsPasswordProtector.Unprotect(account.PasswordCiphertext);
            }
            catch (Exception exception) when (exception is FormatException or System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException("密码副本无法在当前 Windows 用户下解密，请为该账户重置密码。", exception);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UserStoreDocument> LoadOrCreateUnsafeAsync(CancellationToken token)
    {
        if (File.Exists(_storePath))
        {
            await using var input = File.OpenRead(_storePath);
            return await JsonSerializer.DeserializeAsync<UserStoreDocument>(input, _json, token)
                ?? throw new JsonException("用户数据文件为空。");
        }

        var password = PasswordHasher.Hash(BootstrapPassword);
        var administrator = new StoredUserAccount(
            Guid.NewGuid(),
            BootstrapUsername,
            "超级用户",
            password.Hash,
            password.Salt,
            password.Iterations,
            UserRole.Administrator,
            true,
            DateTimeOffset.Now,
            null,
            "首次运行自动创建，请登录后修改默认密码。",
            true,
            0,
            null,
            WindowsPasswordProtector.Protect(BootstrapPassword));
        var document = new UserStoreDocument(1, [administrator]);
        await SaveUnsafeAsync(document, token);
        return document;
    }

    private async Task SaveUnsafeAsync(UserStoreDocument document, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        var temporaryPath = _storePath + ".tmp";
        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(output, document, _json, token);
        }
        File.Move(temporaryPath, _storePath, true);
    }

    private static UserAccountSummary ToSummary(StoredUserAccount user) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.Role,
        user.IsEnabled,
        user.CreatedAt,
        user.LastLoginAt,
        user.Note,
        user.IsProtected);

    private static void ValidateUsername(string username)
    {
        var value = username.Trim();
        if (value.Length is < 3 or > 32
            || value.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("用户名需为 3–32 位，只能包含字母、数字、点、下划线或连字符。");
        }
    }

    private static void ValidateDisplayName(string displayName)
    {
        var value = displayName.Trim();
        if (value.Length is < 1 or > 40) throw new ArgumentException("显示名称需为 1–40 个字符。");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("密码不能为空。");
        if (password.Length > 256) throw new ArgumentException("密码不能超过 256 个字符。");
    }
}
