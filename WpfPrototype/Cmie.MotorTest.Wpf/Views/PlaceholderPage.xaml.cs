using System.Windows;
using System.Windows.Controls;

namespace Cmie.MotorTest.Wpf.Views;

public partial class PlaceholderPage : UserControl
{
    public static readonly DependencyProperty CrumbProperty =
        DependencyProperty.Register(nameof(Crumb), typeof(string), typeof(PlaceholderPage), new PropertyMetadata("工作区"));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PlaceholderPage), new PropertyMetadata("页面"));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(PlaceholderPage),
            new PropertyMetadata("此页内容将在后续阶段按 layout-proposal-v1 复现。"));

    public string Crumb
    {
        get => (string)GetValue(CrumbProperty);
        set => SetValue(CrumbProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public PlaceholderPage()
    {
        InitializeComponent();
    }
}
