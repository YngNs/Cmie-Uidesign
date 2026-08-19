using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Services;

public static class UserSession
{
    public static AuthenticatedUser? Current { get; private set; }

    public static bool IsSignedIn => Current is not null;
    public static bool IsAdministrator => Current?.Role == UserRole.Administrator;
    public static bool CanModifyTests => Current?.Role is UserRole.Administrator or UserRole.Operator;

    public static event Action<AuthenticatedUser?>? Changed;

    public static void SignIn(AuthenticatedUser user)
    {
        Current = user;
        Changed?.Invoke(user);
    }

    public static void SignOut()
    {
        Current = null;
        Changed?.Invoke(null);
    }

    public static void EnsureSignedIn()
    {
        if (!IsSignedIn) throw new InvalidOperationException("请先登录用户账号。");
    }

    public static void EnsureCanModifyTests()
    {
        EnsureSignedIn();
        if (!CanModifyTests) throw new InvalidOperationException("当前只读用户不能修改试验数据。");
    }

    public static void EnsureAdministrator()
    {
        EnsureSignedIn();
        if (!IsAdministrator) throw new InvalidOperationException("该操作仅允许管理员执行。");
    }
}
