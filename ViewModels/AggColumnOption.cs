using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskSum.ViewModels;

public class AggColumnOption : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Key { get; }
    public string DisplayName { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public AggColumnOption(string key, string displayName, bool isChecked = true)
    {
        Key = key;
        DisplayName = displayName;
        _isChecked = isChecked;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
