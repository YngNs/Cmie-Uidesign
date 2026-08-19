using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cmie.MotorTest.Wpf.Models;

public sealed record TestItem(string Key, string Group, string Title, bool IsChild = false);

public sealed class MetricReading : INotifyPropertyChanged
{
    private bool _isPinned;
    private string _value = "";

    public required string Id { get; init; }
    public required string Group { get; init; }
    public required string Label { get; init; }
    public required string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
        }
    }
    public string Unit { get; init; } = "";

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
