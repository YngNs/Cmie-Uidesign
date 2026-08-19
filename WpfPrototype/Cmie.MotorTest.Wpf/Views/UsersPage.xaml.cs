using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cmie.MotorTest.Wpf.Models;
using Cmie.MotorTest.Wpf.Services;

namespace Cmie.MotorTest.Wpf.Views;

public partial class UsersPage : UserControl
{
    private UserAccountSummary? _editing;

    public UsersPage()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<UserAccountRow> Users { get; } = [];
    private MainWindow? Shell => Window.GetWindow(this) as MainWindow;

    public void SetCurrentUser(string name) => CurrentUserText.Text = $"当前用户：{name}";

    public async Task RefreshAsync()
    {
        try
        {
            var selectedId = (UsersGrid.SelectedItem as UserAccountRow)?.Id;
            var accounts = await UserAccountService.Current.ListAsync();
            Users.Clear();
            foreach (var account in accounts) Users.Add(new UserAccountRow(account));
            AccountCountText.Text = accounts.Count.ToString();
            EnabledCountText.Text = accounts.Count(account => account.IsEnabled).ToString();
            AdminCountText.Text = accounts.Count(account => account.Role == UserRole.Administrator).ToString();
            UsersGrid.SelectedItem = Users.FirstOrDefault(user => user.Id == selectedId);
            SetCurrentUser(UserSession.Current?.DisplayName ?? "未登录");
        }
        catch (Exception exception)
        {
            Shell?.ShowToast(exception.Message);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator()) return;
        _editing = null;
        EditorTitle.Text = "新建用户";
        NameInput.Text = string.Empty;
        DisplayNameInput.Text = string.Empty;
        PasswordInput.Password = string.Empty;
        ConfirmPasswordInput.Password = string.Empty;
        NoteInput.Text = string.Empty;
        PasswordHint.Text = "密码不限制复杂度，只需非空。";
        RoleInput.SelectedIndex = 1;
        StatusInput.SelectedIndex = 0;
        NameInput.IsEnabled = true;
        OpenEditor();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator()) return;
        if (UsersGrid.SelectedItem is not UserAccountRow row) { Shell?.ShowToast("请先选择用户"); return; }
        var user = row.Account;
        _editing = user;
        EditorTitle.Text = "编辑用户";
        NameInput.Text = user.Username;
        DisplayNameInput.Text = user.DisplayName;
        PasswordInput.Password = string.Empty;
        ConfirmPasswordInput.Password = string.Empty;
        NoteInput.Text = user.Note;
        PasswordHint.Text = "不修改密码时请保持两项为空。";
        RoleInput.SelectedIndex = user.Role switch { UserRole.Administrator => 0, UserRole.Operator => 1, _ => 2 };
        StatusInput.SelectedIndex = user.IsEnabled ? 0 : 1;
        NameInput.IsEnabled = false;
        OpenEditor();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator()) return;
        if (UsersGrid.SelectedItem is not UserAccountRow row) { Shell?.ShowToast("请先选择用户"); return; }
        var user = row.Account;
        try
        {
            await UserAccountService.Current.DeleteAsync(user.Id);
            await RefreshAsync();
            Shell?.ShowToast($"已删除用户 {user.DisplayName}");
        }
        catch (Exception exception) { Shell?.ShowToast(exception.Message); }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator() || sender is not FrameworkElement { DataContext: UserAccountRow row }) return;
        if (row.IsPasswordVisible)
        {
            row.HidePassword();
            return;
        }

        try
        {
            var password = await UserAccountService.Current.RevealPasswordAsync(row.Id);
            if (password is null)
            {
                Shell?.ShowToast("该旧账户没有可显示的密码副本，请编辑账户并重置一次密码");
                return;
            }
            row.ShowPassword(password);
        }
        catch (Exception exception) { Shell?.ShowToast(exception.Message); }
    }

    private bool RequireAdministrator()
    {
        try { UserSession.EnsureAdministrator(); return true; }
        catch (Exception exception) { Shell?.ShowToast(exception.Message); return false; }
    }

    private void OpenEditor() { EditorOverlay.Visibility = Visibility.Visible; Focus(); Dispatcher.BeginInvoke(() => NameInput.Focus()); }
    private void CloseEditor() { EditorOverlay.Visibility = Visibility.Collapsed; _editing = null; }
    private void CancelEditor_Click(object sender, RoutedEventArgs e) => CloseEditor();
    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => CloseEditor();
    private void Editor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;
    private void Root_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape && EditorOverlay.Visibility == Visibility.Visible) { CloseEditor(); e.Handled = true; } }

    private async void SaveEditor_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdministrator()) return;
        var password = PasswordInput.Password;
        if (password != ConfirmPasswordInput.Password) { Shell?.ShowToast("两次输入的密码不一致"); return; }

        try
        {
            var role = RoleInput.SelectedIndex switch { 0 => UserRole.Administrator, 1 => UserRole.Operator, _ => UserRole.ReadOnly };
            var isEnabled = StatusInput.SelectedIndex == 0;
            if (_editing is null)
            {
                await UserAccountService.Current.CreateAsync(new CreateUserRequest(
                    NameInput.Text, DisplayNameInput.Text, password, role, isEnabled, NoteInput.Text));
                Shell?.ShowToast("用户已创建");
            }
            else
            {
                await UserAccountService.Current.UpdateAsync(new UpdateUserRequest(
                    _editing.Id, DisplayNameInput.Text, role, isEnabled, NoteInput.Text,
                    string.IsNullOrEmpty(password) ? null : password));
                Shell?.ShowToast("用户信息已更新");
            }

            CloseEditor();
            await RefreshAsync();
        }
        catch (Exception exception) { Shell?.ShowToast(exception.Message); }
    }

    public sealed class UserAccountRow : INotifyPropertyChanged
    {
        private string? _password;
        private bool _isPasswordVisible;

        public UserAccountRow(UserAccountSummary account) => Account = account;

        public UserAccountSummary Account { get; }
        public Guid Id => Account.Id;
        public string Username => Account.Username;
        public string DisplayName => Account.DisplayName;
        public string Initial => Account.Initial;
        public string AccountMeta => Account.AccountMeta;
        public string RoleDisplay => Account.RoleDisplay;
        public string StatusDisplay => Account.StatusDisplay;
        public string LastLoginDisplay => Account.LastLoginDisplay;
        public string Note => Account.Note;
        public bool IsEnabled => Account.IsEnabled;
        public bool IsProtected => Account.IsProtected;
        public string PasswordDisplay => _isPasswordVisible ? _password ?? "不可用" : "••••••••";
        public string PasswordButtonText => _isPasswordVisible ? "隐藏" : "显示";
        public bool IsPasswordVisible => _isPasswordVisible;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void ShowPassword(string password)
        {
            _password = password;
            _isPasswordVisible = true;
            NotifyPasswordChanged();
        }

        public void HidePassword()
        {
            _password = null;
            _isPasswordVisible = false;
            NotifyPasswordChanged();
        }

        private void NotifyPasswordChanged()
        {
            OnPropertyChanged(nameof(PasswordDisplay));
            OnPropertyChanged(nameof(PasswordButtonText));
            OnPropertyChanged(nameof(IsPasswordVisible));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
