namespace TaskSum.Models;

public class AggregationItem
{
    public string Activity { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalOriginalEstimate { get; set; }
    public double TotalRemainingWork { get; set; }
    public double TotalCompletedWork { get; set; }
    public bool IsTotal { get; set; }
}
