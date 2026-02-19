namespace TaskSum.Models;

public class WorkItemData
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public double? OriginalEstimate { get; set; }
    public double? RemainingWork { get; set; }
    public double? CompletedWork { get; set; }
    public bool? IsReview { get; set; }
    public string DevelopProcess { get; set; } = string.Empty;
}
