using System.Windows;

namespace Cmie.MotorTest.Wpf.Services;

/// <summary>
/// 对应 layout-proposal-v1.html 的 showToast。
/// </summary>
public static class AppToast
{
    public static void Show(string text)
    {
        if (Application.Current.MainWindow is MainWindow window)
        {
            window.ShowToast(text);
        }
    }
}
