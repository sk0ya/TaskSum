using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskSum.Models;

namespace TaskSum.ViewModels;

public class WorkItemNodeViewModel : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public int Id { get; }
    public string Title { get; }
    public string WorkItemType { get; }
    public string State { get; }
    public string AssignedTo { get; }
    public string Activity { get; }
    public double? OriginalEstimate { get; }
    public double? RemainingWork { get; }
    public double? CompletedWork { get; }
    public bool? IsReview { get; }
    public string DevelopProcess { get; }

    public int Level { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public List<WorkItemNodeViewModel> Children { get; } = [];
    public WorkItemNodeViewModel? Parent { get; set; }

    public bool HasChildren => Children.Count > 0;

    public WorkItemNodeViewModel(WorkItemData data)
    {
        Id = data.Id;
        Title = data.Title;
        WorkItemType = data.WorkItemType;
        State = data.State;
        AssignedTo = data.AssignedTo;
        Activity = data.Activity;
        OriginalEstimate = data.OriginalEstimate;
        RemainingWork = data.RemainingWork;
        CompletedWork = data.CompletedWork;
        IsReview = data.IsReview;
        DevelopProcess = data.DevelopProcess;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
