using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskSum.ViewModels;

public class FilterOption : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Name { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; OnPropertyChanged(); }
    }

    public FilterOption(string name, bool isChecked = false)
    {
        Name = name;
        _isChecked = isChecked;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
