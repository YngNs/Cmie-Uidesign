using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Cmie.MotorTest.Wpf.Dialogs;

public partial class LoginOverlay : UserControl
{
    private static readonly Color Accent = (Color)ColorConverter.ConvertFromString("#0284C7");
    private static readonly Color LineIdle = (Color)ColorConverter.ConvertFromString("#1A0F172A");
    private static readonly Color TextMain = (Color)ColorConverter.ConvertFromString("#0F172A");
    private static readonly Color TextMuted = (Color)ColorConverter.ConvertFromString("#64748B");

    private bool _passwordVisible;
    private bool _lineFrequencyMotor = true;

    public event Action<string, string>? Submitted;
    public event Action? Cancelled;
    public event Action<string>? ValidationFailed;

    public string MotorType => _lineFrequencyMotor ? "工频电机" : "变频电机";

    public LoginOverlay()
    {
        InitializeComponent();
    }

    public void PrepareOpen()
    {
        PasswordBox.Password = string.Empty;
        PasswordPlainBox.Text = string.Empty;
        SetPasswordVisible(false);
        SelectMotor(lineFrequency: true);
        SetFieldActive(UserFieldBorder, UserFieldGlow, true);
        SetFieldActive(PassFieldBorder, PassFieldGlow, false);
        ClearMessage();
        SetBusy(false);
    }

    public void ShowMessage(string message, bool isError = true)
    {
        LoginStatusText.Text = message;
        LoginStatusText.Foreground = new SolidColorBrush(isError
            ? (Color)ColorConverter.ConvertFromString("#DC2626")
            : (Color)ColorConverter.ConvertFromString("#0284C7"));
        LoginStatusPanel.Background = new SolidColorBrush(isError
            ? Color.FromArgb(0x14, 0xDC, 0x26, 0x26)
            : Color.FromArgb(0x14, 0x02, 0x84, 0xC7));
        LoginStatusPanel.BorderBrush = new SolidColorBrush(isError
            ? Color.FromArgb(0x44, 0xDC, 0x26, 0x26)
            : Color.FromArgb(0x44, 0x02, 0x84, 0xC7));
        LoginStatusPanel.Visibility = Visibility.Visible;
    }

    public void ClearMessage()
    {
        LoginStatusText.Text = string.Empty;
        LoginStatusPanel.Visibility = Visibility.Collapsed;
    }

    public void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        UsernameBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        PasswordPlainBox.IsEnabled = !busy;
        LoginButtonText.Text = busy ? "正在登录…" : "进入";
        if (busy) ShowMessage("正在验证账户，请稍候…", isError: false);
    }

    public void FocusUsername()
    {
        UsernameBox.Focus();
        UsernameBox.SelectAll();
    }

    private void Enter_Click(object sender, RoutedEventArgs e)
    {
        ClearMessage();
        var name = UsernameBox.Text.Trim();
        var pwd = _passwordVisible ? PasswordPlainBox.Text : PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowMessage("请输入用户名");
            ValidationFailed?.Invoke("请输入用户名");
            UsernameBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(pwd))
        {
            ShowMessage("请输入密码");
            ValidationFailed?.Invoke("请输入密码");
            if (_passwordVisible)
            {
                PasswordPlainBox.Focus();
            }
            else
            {
                PasswordBox.Focus();
            }

            return;
        }

        Submitted?.Invoke(name, pwd);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke();

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = false;

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void UserField_GotFocus(object sender, RoutedEventArgs e) =>
        SetFieldActive(UserFieldBorder, UserFieldGlow, true);

    private void UserField_LostFocus(object sender, RoutedEventArgs e) =>
        SetFieldActive(UserFieldBorder, UserFieldGlow, false);

    private void PassField_GotFocus(object sender, RoutedEventArgs e) =>
        SetFieldActive(PassFieldBorder, PassFieldGlow, true);

    private void PassField_LostFocus(object sender, RoutedEventArgs e) =>
        SetFieldActive(PassFieldBorder, PassFieldGlow, false);

    private static void SetFieldActive(Border border, DropShadowEffect glow, bool active)
    {
        border.BorderBrush = new SolidColorBrush(active ? Accent : LineIdle);
        glow.Opacity = active ? 0.28 : 0;
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e) =>
        SetPasswordVisible(!_passwordVisible);

    private void SetPasswordVisible(bool visible)
    {
        _passwordVisible = visible;
        if (visible)
        {
            PasswordPlainBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordPlainBox.Visibility = Visibility.Visible;
            PasswordPlainBox.CaretIndex = PasswordPlainBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = PasswordPlainBox.Text;
            PasswordPlainBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
        }
    }

    private void MotorLine_Click(object sender, MouseButtonEventArgs e) => SelectMotor(true);

    private void MotorVfd_Click(object sender, MouseButtonEventArgs e) => SelectMotor(false);

    private void SelectMotor(bool lineFrequency)
    {
        _lineFrequencyMotor = lineFrequency;
        ApplyMotorChrome(MotorLineBorder, selected: lineFrequency);
        ApplyMotorChrome(MotorVfdBorder, selected: !lineFrequency);
    }

    private static void ApplyMotorChrome(Border border, bool selected)
    {
        if (selected)
        {
            border.BorderBrush = new SolidColorBrush(Accent);
            border.BorderThickness = new Thickness(1.5);
            border.Background = new SolidColorBrush(Color.FromArgb(0x14, 0x02, 0x8A, 0xC8));
            border.Effect = new DropShadowEffect
            {
                Color = Accent,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.22
            };
        }
        else
        {
            border.BorderBrush = new SolidColorBrush(LineIdle);
            border.BorderThickness = new Thickness(1);
            border.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
            border.Effect = null;
        }

        if (border.Child is StackPanel sp && sp.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
        {
            label.Foreground = new SolidColorBrush(selected ? TextMain : TextMuted);
        }
    }
}
