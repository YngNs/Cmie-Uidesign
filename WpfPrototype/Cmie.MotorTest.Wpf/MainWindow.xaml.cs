using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Cmie.MotorTest.Wpf.Services;
using Cmie.MotorTest.Wpf.Views;

namespace Cmie.MotorTest.Wpf;

public partial class MainWindow : Window
{
    private static readonly DependencyProperty SidebarWidthProxyProperty =
        DependencyProperty.Register(
            nameof(SidebarWidthProxy),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(208.0, OnSidebarWidthProxyChanged));

    private bool _darkMode;
    private bool _sideCollapsed;
    private bool _sidebarAnimating;
    private bool _loginInProgress;
    private string _currentUser = "未登录";
    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly List<FrameworkElement> _sideLabels = new();
    private readonly DispatcherTimer _toastTimer;

    public string CurrentUserDisplay =>
        string.IsNullOrWhiteSpace(_currentUser) ? "未登录" : _currentUser;

    private double SidebarWidthProxy
    {
        get => (double)GetValue(SidebarWidthProxyProperty);
        set => SetValue(SidebarWidthProxyProperty, value);
    }

    private static void OnSidebarWidthProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MainWindow)d).SidebarColumn.Width = new GridLength((double)e.NewValue);
    }

    private static string SideCollapseSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cmie.MotorTest.Wpf",
            "side-collapsed");

    public MainWindow()
    {
        InitializeComponent();

        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1600)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            HideToast();
        };

        _sideLabels.AddRange(
        [
            SideWorkspaceLabel,
            BrandTextPanel,
            NavHomeLabel,
            NavProjectLabel,
            NavRealtimeLabel,
            NavSettingsLabel,
            NavReportLabel,
            CollapseMenuLabel,
            SideNavSeparator
        ]);

        RegisterPages();
        RegisterNavButtons();
        ApplyTheme(false);
        ThemeButton.Content = "☾";
        ThemeButton.ToolTip = "切换深色主题";
        Navigate("home");
        SetSideCollapsed(LoadSideCollapsed(), persist: false, animate: false);
        UpdateUserChrome();
        Loaded += async (_, _) => await RefreshBackendStatusAsync();
    }

    private async Task RefreshBackendStatusAsync()
    {
        try
        {
            var status = await LocalDataService.Current.GetStatusAsync();
            BackendStatusText.Text = $"数据存储: 本地 · {status.CheckedAt:HH:mm:ss}";
            BackendStatusDot.Fill = (Brush)FindResource("GoodBrush");
            BackendStatusText.ToolTip = status.DataDirectory;
        }
        catch
        {
            BackendStatusText.Text = "本地存储: 不可用";
            BackendStatusDot.Fill = (Brush)FindResource("BadBrush");
            BackendStatusText.ToolTip = "请检查本地数据目录访问权限";
        }
    }

    private void RegisterPages()
    {
        _pages["home"] = new HomePage();
        _pages["new-test"] = new NewTestPage();
        _pages["project"] = new ProjectPage();
        _pages["realtime"] = new RealtimePage();
        _pages["settings"] = new SettingsPage();
        _pages["report"] = new ReportPage();
        _pages["users"] = new UsersPage();
    }

    private void RegisterNavButtons()
    {
        _navButtons["home"] = NavHomeButton;
        _navButtons["project"] = NavProjectButton;
        _navButtons["realtime"] = NavRealtimeButton;
        _navButtons["settings"] = NavSettingsButton;
        _navButtons["report"] = NavReportButton;
    }

    public void Navigate(string pageKey)
    {
        if (!_pages.TryGetValue(pageKey, out var page))
        {
            return;
        }

        if (pageKey == "project" && page is ProjectPage projectPage)
        {
            projectPage.RefreshProject();
        }

        if (pageKey == "home" && page is HomePage homePage)
        {
            homePage.RefreshHeroCopy();
        }

        if (pageKey == "users" && page is UsersPage usersPage)
        {
            usersPage.SetCurrentUser(CurrentUserDisplay);
        }

        PageHost.Content = page;
        UpdateNavActive(pageKey);
    }

    public void ShowToast(string text)
    {
        ToastText.Text = text;
        _toastTimer.Stop();

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new ThicknessAnimation(
            new Thickness(0, 0, 0, 52),
            new Thickness(0, 0, 0, 64),
            TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        ToastBanner.BeginAnimation(OpacityProperty, fadeIn);
        ToastBanner.BeginAnimation(MarginProperty, slide);
        _toastTimer.Start();
    }

    private void HideToast()
    {
        var fadeOut = new DoubleAnimation(ToastBanner.Opacity, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        ToastBanner.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void UpdateNavActive(string pageKey)
    {
        var activeStyle = (Style)FindResource("SideActiveButtonStyle");
        var normalStyle = (Style)FindResource("SideButtonStyle");

        foreach (var (key, button) in _navButtons)
        {
            button.Style = key == pageKey ? activeStyle : normalStyle;
            ApplySideButtonLayout(button);
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            Navigate(key);
        }
    }

    private void Brand_Click(object sender, MouseButtonEventArgs e)
    {
        Navigate("home");
    }

    private Popup? _menuPopupClosedByClick;

    private void OpMenuButton_Click(object sender, RoutedEventArgs e) => ToggleMenuPopup(OpMenuPopup);

    private void HelpMenuButton_Click(object sender, RoutedEventArgs e) => ToggleMenuPopup(HelpMenuPopup);

    private void UserMenuButton_Click(object sender, RoutedEventArgs e) => ToggleMenuPopup(UserMenuPopup);

    private void MenuPopup_Closed(object sender, EventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }

        // 点同一按钮关闭时：Popup 先关，随后 Click 又会打开；用这一帧忽略重开
        _menuPopupClosedByClick = popup;
        Dispatcher.BeginInvoke(() =>
        {
            if (_menuPopupClosedByClick == popup)
            {
                _menuPopupClosedByClick = null;
            }
        }, DispatcherPriority.Input);
    }

    private void ToggleMenuPopup(Popup popup)
    {
        if (_menuPopupClosedByClick == popup)
        {
            _menuPopupClosedByClick = null;
            return;
        }

        CloseAllMenuPopups();
        popup.IsOpen = true;
    }

    private void CloseAllMenuPopups()
    {
        OpMenuPopup.IsOpen = false;
        HelpMenuPopup.IsOpen = false;
        UserMenuPopup.IsOpen = false;
    }

    private void TopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CloseAllMenuPopups();
        if (sender is not FrameworkElement { Tag: string action })
        {
            return;
        }

        switch (action)
        {
            case "新建试验":
                Navigate("new-test");
                break;
            case "上次试验":
            case "指定试验":
                Navigate("project");
                ShowToast($"已打开「{action}」");
                break;
            case "修改项目参数":
                ShowToast("触发操作：修改项目参数（流程后续细化）");
                break;
            case "退出当前试验项目":
                Navigate("home");
                ShowToast("已退出当前试验项目（演示）");
                break;
            case "使用说明":
                ShowToast("使用说明（弹窗后续细化）");
                break;
            case "关于软件":
                ShowToast("三相异步电机测试软件 · WPF 视觉原型");
                break;
            default:
                ShowToast($"触发操作：{action}");
                break;
        }
    }

    private void UserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CloseAllMenuPopups();
        if (sender is not FrameworkElement { Tag: string action })
        {
            return;
        }

        switch (action)
        {
            case "用户登录":
                OpenLoginOverlay();
                break;
            case "用户注销":
                UserSession.SignOut();
                _currentUser = "未登录";
                UpdateUserChrome();
                ShowToast("已注销当前用户");
                break;
            case "用户管理":
                try
                {
                    UserSession.EnsureAdministrator();
                    Navigate("users");
                    ShowToast("已打开「用户管理」");
                }
                catch (Exception exception)
                {
                    ShowToast(exception.Message);
                }
                break;
            case "退出程序":
                ShowToast("退出程序（原型演示）");
                Close();
                break;
        }
    }

    private void OpenLoginOverlay()
    {
        LoginOverlay.PrepareOpen();
        LoginOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() => LoginOverlay.FocusUsername(), DispatcherPriority.Input);
    }

    private void CloseLoginOverlay()
    {
        LoginOverlay.Visibility = Visibility.Collapsed;
    }

    private async void LoginOverlay_Submitted(string name, string pwd)
    {
        if (_loginInProgress) return;
        _loginInProgress = true;
        LoginOverlay.SetBusy(true);
        try
        {
            var user = await UserAccountService.Current.AuthenticateAsync(name, pwd);
            UserSession.SignIn(user);
            Models.DemoSession.MotorType = LoginOverlay.MotorType;
            _currentUser = user.DisplayName;
            UpdateUserChrome();
            CloseLoginOverlay();
            Navigate("home");
            ShowToast($"登录成功：{user.DisplayName}（{LoginOverlay.MotorType}）");
            if (PageHost.Content is HomePage homePage)
            {
                homePage.RefreshHeroCopy();
            }
        }
        catch (Exception exception)
        {
            LoginOverlay.ShowMessage(exception.Message);
            ShowToast(exception.Message);
        }
        finally
        {
            _loginInProgress = false;
            LoginOverlay.SetBusy(false);
        }
    }

    private void LoginOverlay_Cancelled() => CloseLoginOverlay();

    private void LoginOverlay_ValidationFailed(string message)
    {
        LoginOverlay.ShowMessage(message);
        ShowToast(message);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && LoginOverlay.Visibility == Visibility.Visible)
        {
            CloseLoginOverlay();
            e.Handled = true;
        }
    }

    private void UpdateUserChrome()
    {
        var display = string.IsNullOrWhiteSpace(_currentUser) ? "未登录" : _currentUser;
        UserNameText.Text = $"{display}  ▾";
        UserAvatarText.Text = display[..1];
        UserAccountHeader.Text = $"当前账户 · {display}";
    }

    private void CollapseMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sidebarAnimating)
        {
            return;
        }

        SetSideCollapsed(!_sideCollapsed);
    }

    private void SetSideCollapsed(bool collapsed, bool persist = true, bool animate = true)
    {
        _sideCollapsed = collapsed;
        CollapseMenuIcon.Text = collapsed ? "›" : "‹";
        CollapseMenuButton.ToolTip = collapsed ? "展开菜单" : "收起菜单";
        CollapseMenuLabel.Text = collapsed ? "展开菜单" : "收起菜单";

        // 布局始终左对齐固定边距，只做宽度/透明度动画，避免 logo 先居中再收缩造成跳动
        BrandRoot.Margin = new Thickness(16, 0, 16, 0);
        BrandRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        BrandRoot.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

        foreach (var button in _navButtons.Values)
        {
            ApplySideButtonLayout(button);
        }

        ApplySideButtonLayout(CollapseMenuButton);

        if (!animate)
        {
            BeginAnimation(SidebarWidthProxyProperty, null);
            SidebarWidthProxy = collapsed ? 72 : 208;
            ApplyLabelVisuals(collapsed, animate: false);
            if (persist)
            {
                SaveSideCollapsed(collapsed);
            }

            return;
        }

        _sidebarAnimating = true;
        var targetWidth = collapsed ? 72.0 : 208.0;
        var currentWidth = SidebarColumn.Width.IsAbsolute
            ? SidebarColumn.Width.Value
            : SidebarWidthProxy;

        // 宽度与文字透明度同一时段并行，只有一条过渡曲线
        var duration = TimeSpan.FromMilliseconds(260);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        ApplyLabelVisuals(collapsed, animate: true, duration, ease);

        var widthAnim = new DoubleAnimation(currentWidth, targetWidth, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        widthAnim.Completed += (_, _) =>
        {
            BeginAnimation(SidebarWidthProxyProperty, null);
            SidebarWidthProxy = targetWidth;
            ApplyLabelVisuals(collapsed, animate: false);
            _sidebarAnimating = false;
            if (persist)
            {
                SaveSideCollapsed(collapsed);
            }
        };
        BeginAnimation(SidebarWidthProxyProperty, widthAnim);
    }

    private void ApplyLabelVisuals(
        bool collapsed,
        bool animate,
        TimeSpan? duration = null,
        IEasingFunction? ease = null)
    {
        var targetOpacity = collapsed ? 0.0 : 1.0;
        var targetMaxHeight = collapsed ? 0.0 : 24.0;
        var targetMargin = collapsed ? new Thickness(22, 0, 0, 0) : new Thickness(22, 0, 0, 8);

        foreach (var label in _sideLabels)
        {
            label.Visibility = Visibility.Visible;
            label.IsHitTestVisible = !collapsed;
            label.BeginAnimation(UIElement.OpacityProperty, null);

            if (!animate || duration is null)
            {
                label.Opacity = targetOpacity;
                continue;
            }

            var opacityAnim = new DoubleAnimation(label.Opacity, targetOpacity, duration.Value)
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };
            var captured = label;
            opacityAnim.Completed += (_, _) =>
            {
                captured.BeginAnimation(UIElement.OpacityProperty, null);
                captured.Opacity = targetOpacity;
            };
            label.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        SideWorkspaceLabel.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
        SideWorkspaceLabel.BeginAnimation(FrameworkElement.MarginProperty, null);

        if (!animate || duration is null)
        {
            SideWorkspaceLabel.MaxHeight = targetMaxHeight;
            SideWorkspaceLabel.Margin = targetMargin;
            SideWorkspaceLabel.Opacity = targetOpacity;
            return;
        }

        var maxHeightAnim = new DoubleAnimation(SideWorkspaceLabel.MaxHeight, targetMaxHeight, duration.Value)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        maxHeightAnim.Completed += (_, _) =>
        {
            SideWorkspaceLabel.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
            SideWorkspaceLabel.MaxHeight = targetMaxHeight;
        };
        SideWorkspaceLabel.BeginAnimation(FrameworkElement.MaxHeightProperty, maxHeightAnim);

        var marginAnim = new ThicknessAnimation(SideWorkspaceLabel.Margin, targetMargin, duration.Value)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };
        marginAnim.Completed += (_, _) =>
        {
            SideWorkspaceLabel.BeginAnimation(FrameworkElement.MarginProperty, null);
            SideWorkspaceLabel.Margin = targetMargin;
        };
        SideWorkspaceLabel.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
    }

    private void ApplySideButtonLayout(Button button)
    {
        // 展开/收起共用同一套左对齐布局，图标位置不跳动；收窄时靠 ClipToBounds 裁切文字
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Padding = new Thickness(13, 0, 13, 0);
        button.Margin = new Thickness(10, 2, 10, 2);
    }

    private static bool LoadSideCollapsed()
    {
        try
        {
            return File.Exists(SideCollapseSettingsPath)
                   && File.ReadAllText(SideCollapseSettingsPath).Trim() == "1";
        }
        catch
        {
            return false;
        }
    }

    private static void SaveSideCollapsed(bool collapsed)
    {
        try
        {
            var dir = Path.GetDirectoryName(SideCollapseSettingsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(SideCollapseSettingsPath, collapsed ? "1" : "0");
        }
        catch
        {
            // ignore persistence failures in prototype
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _darkMode = !_darkMode;
        ApplyTheme(_darkMode);
        ThemeButton.Content = _darkMode ? "☀" : "☾";
        ThemeButton.ToolTip = _darkMode ? "切换浅色主题" : "切换深色主题";
        ShowToast(_darkMode ? "已切换为深色主题" : "已切换为浅色主题");
    }

    private void ApplyTheme(bool dark)
    {
        if (dark)
        {
            // Cyber Lab（与登录页同系）
            SetBrush("AppBackground", "#03060C");
            SetBrush("PanelBrush", "#0D1928");
            SetBrush("SidebarBrush", "#070B12");
            SetBrush("SidebarActiveBrush", "#122033");
            SetBrush("HeaderBrush", "#0A1018");
            SetBrush("StatusBrush", "#0A1018");
            SetBrush("TextBrush", "#E8F1F8");
            SetBrush("MutedBrush", "#7A95A8");
            SetBrush("LineBrush", "#80516F8A");
            SetBrush("LineSoftBrush", "#4D4B6780");
            SetBrush("AccentBrush", "#00D4FF");
            SetBrush("AccentBrightBrush", "#00E5FF");
            SetBrush("AccentSoftBrush", "#2200D4FF");
            SetBrush("GoodBrush", "#4ADE80");
            SetBrush("BadBrush", "#F87171");
            SetBrush("WarnBrush", "#FBBF24");
            SetBrush("MetricBrush", "#07111D");
            SetBrush("ToastBrush", "#F50A121C");
            SetBrush("DropdownBrush", "#0B1524");
            SetBrush("DropdownHoverBrush", "#2800D4FF");
            SetBrush("ScrollTrackBrush", "#B307111D");
            SetBrush("ScrollThumbBrush", "#A34B6780");
            SetBrush("ScrollThumbHoverBrush", "#00D4FF");
            SetBrush("PrimaryButtonBrush", "#0E7490");
            SetBrush("PrimaryButtonHoverBrush", "#0F8AA6");
        }
        else
        {
            // 浅色仍用青蓝强调，避免完全割裂
            SetBrush("AppBackground", "#E8EEF4");
            SetBrush("PanelBrush", "#FFFFFF");
            SetBrush("SidebarBrush", "#0A1018");
            SetBrush("SidebarActiveBrush", "#122033");
            SetBrush("HeaderBrush", "#FFFFFF");
            SetBrush("StatusBrush", "#FFFFFF");
            SetBrush("TextBrush", "#0F172A");
            SetBrush("MutedBrush", "#64748B");
            SetBrush("LineBrush", "#1A0F172A");
            SetBrush("LineSoftBrush", "#0F0F172A");
            SetBrush("AccentBrush", "#0284C7");
            SetBrush("AccentBrightBrush", "#06B6D4");
            SetBrush("AccentSoftBrush", "#1900D4FF");
            SetBrush("GoodBrush", "#16A34A");
            SetBrush("BadBrush", "#DC2626");
            SetBrush("WarnBrush", "#D97706");
            SetBrush("MetricBrush", "#F1F5F9");
            SetBrush("ToastBrush", "#EB1A2332");
            SetBrush("DropdownBrush", "#FFFFFF");
            SetBrush("DropdownHoverBrush", "#1400D4FF");
            SetBrush("ScrollTrackBrush", "#0A0F172A");
            SetBrush("ScrollThumbBrush", "#5264748B");
            SetBrush("ScrollThumbHoverBrush", "#0284C7");
            SetBrush("PrimaryButtonBrush", "#0284C7");
            SetBrush("PrimaryButtonHoverBrush", "#0369A1");
        }
    }

    private static void SetBrush(string key, string color)
    {
        Application.Current.Resources[key] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }
}
