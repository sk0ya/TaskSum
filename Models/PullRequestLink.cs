namespace TaskSum.Models;

public class PullRequestLink
{
    public int PrId { get; set; }
    public string RepoGuid { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
}
