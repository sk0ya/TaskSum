namespace TaskSum.Models;

public class AggregationItem
{
    public string Activity { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalOriginalEstimate { get; set; }
    public double TotalRemainingWork { get; set; }
    public double TotalCompletedWork { get; set; }

    // IsReview=True のみ
    public double ReviewOriginalEstimate { get; set; }
    public double ReviewRemainingWork { get; set; }
    public double ReviewCompletedWork { get; set; }

    // IsReview=False または null (非レビュー)
    public double NonReviewOriginalEstimate { get; set; }
    public double NonReviewRemainingWork { get; set; }
    public double NonReviewCompletedWork { get; set; }

    public bool IsTotal { get; set; }
}
