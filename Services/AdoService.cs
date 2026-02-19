using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskSum.Models;

namespace TaskSum.Services;

public class AdoService
{
    private readonly HttpClient _httpClient;
    private readonly string _orgUrl;
    private readonly string _project;

    private static readonly string[] WorkItemFields =
    [
        "System.Id",
        "System.Title",
        "System.WorkItemType",
        "System.State",
        "System.AssignedTo",
        "Microsoft.VSTS.Common.Activity",
        "Microsoft.VSTS.Scheduling.OriginalEstimate",
        "Microsoft.VSTS.Scheduling.RemainingWork",
        "Microsoft.VSTS.Scheduling.CompletedWork",
        "Custom.IsReview",
        "Custom.DevelopProcess",
    ];

    public AdoService(string orgUrl, string project, string pat)
    {
        _orgUrl = orgUrl.TrimEnd('/');
        _project = Uri.EscapeDataString(project.Trim());

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// WIQL (Recursive) で Feature 配下の全リンクを取得します。
    /// 戻り値: (sourceId, targetId) のリスト。rootの場合 sourceId = 0
    /// </summary>
    public async Task<List<(int sourceId, int targetId)>> GetDescendantLinksAsync(int featureId)
    {
        var wiql = new
        {
            query =
                $"SELECT [System.Id] FROM WorkItemLinks " +
                $"WHERE [Source].[System.Id] = {featureId} " +
                $"AND [System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward' " +
                $"MODE (Recursive)"
        };

        var body = new StringContent(
            JsonSerializer.Serialize(wiql), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_orgUrl}/{_project}/_apis/wit/wiql?api-version=7.0", body);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var links = new List<(int, int)>();

        if (!doc.RootElement.TryGetProperty("workItemRelations", out var relations))
            return links;

        foreach (var rel in relations.EnumerateArray())
        {
            int srcId = 0, tgtId = 0;

            if (rel.TryGetProperty("source", out var src) && src.ValueKind != JsonValueKind.Null)
                srcId = src.GetProperty("id").GetInt32();

            if (rel.TryGetProperty("target", out var tgt) && tgt.ValueKind != JsonValueKind.Null)
                tgtId = tgt.GetProperty("id").GetInt32();

            if (tgtId > 0)
                links.Add((srcId, tgtId));
        }

        return links;
    }

    /// <summary>
    /// ID リストを 200 件ずつバッチで並列取得します（最大 5 同時リクエスト）。
    /// </summary>
    public async Task<List<WorkItemData>> GetWorkItemsAsync(IEnumerable<int> ids, IProgress<int>? progress = null)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];

        var fieldsParam = string.Join(",", WorkItemFields);

        // バッチに分割
        var batches = new List<int[]>();
        for (int i = 0; i < idList.Count; i += 200)
            batches.Add(idList.Skip(i).Take(200).ToArray());

        var results = new WorkItemData[batches.Count][];
        var semaphore = new SemaphoreSlim(5);
        int fetched = 0;

        var tasks = batches.Select(async (batch, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var idsStr = string.Join(",", batch);
                var response = await _httpClient.GetAsync(
                    $"{_orgUrl}/{_project}/_apis/wit/workitems" +
                    $"?ids={idsStr}&fields={fieldsParam}&api-version=7.0");

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var items = new List<WorkItemData>();
                if (doc.RootElement.TryGetProperty("value", out var value))
                    foreach (var item in value.EnumerateArray())
                        items.Add(ParseWorkItem(item));

                results[index] = [.. items];

                var done = Interlocked.Add(ref fetched, batch.Length);
                progress?.Report(done);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        return results.SelectMany(r => r ?? []).ToList();
    }

    /// <summary>
    /// オーガナイゼーション内の全Gitリポジトリを取得します。
    /// 戻り値: repoGuid -> (repoName, projectName)
    /// </summary>
    public async Task<Dictionary<string, (string RepoName, string ProjectName)>> GetRepositoriesAsync()
    {
        var response = await _httpClient.GetAsync(
            $"{_orgUrl}/_apis/git/repositories?api-version=7.0");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

        if (!doc.RootElement.TryGetProperty("value", out var value))
            return result;

        foreach (var repo in value.EnumerateArray())
        {
            if (!repo.TryGetProperty("id", out var idEl)) continue;
            if (!repo.TryGetProperty("name", out var nameEl)) continue;

            var id = idEl.GetString() ?? string.Empty;
            var name = nameEl.GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(id)) continue;

            var projectName = string.Empty;
            if (repo.TryGetProperty("project", out var projEl) &&
                projEl.TryGetProperty("name", out var projNameEl))
                projectName = projNameEl.GetString() ?? string.Empty;

            result[id] = (name, projectName);
        }

        return result;
    }

    /// <summary>
    /// WorkItem の relations から直接リンクされた PR を取得します。
    /// single-item エンドポイントで $expand=relations を使用します。
    /// </summary>
    public async Task<List<PullRequestLink>> GetPullRequestsForWorkItemAsync(
        int workItemId,
        Dictionary<string, (string RepoName, string ProjectName)> repoMap)
    {
        var response = await _httpClient.GetAsync(
            $"{_orgUrl}/{_project}/_apis/wit/workitems/{workItemId}?$expand=relations&api-version=7.0");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var prLinks = new List<PullRequestLink>();

        if (!doc.RootElement.TryGetProperty("relations", out var relations))
            return prLinks;

        // Azure DevOps が返す artifact URL 形式:
        //   vstfs:///Git/PullRequestId/{projectGuid}%2F{repoGuid}%2F{prId}
        // (%2F は URL エンコードされた '/')
        // デコード後: 最後のセグメント = prId、その前 = repoGuid
        const string prPrefix = "vstfs:///Git/PullRequestId/";
        foreach (var rel in relations.EnumerateArray())
        {
            if (!rel.TryGetProperty("url", out var urlEl)) continue;
            var artifactUrl = urlEl.GetString() ?? string.Empty;
            if (!artifactUrl.StartsWith(prPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            // Azure DevOps は artifact ID 内の区切りを %2F でエンコードして返すため、
            // 先に URL デコードしてから '/' で分割する。
            var decoded = Uri.UnescapeDataString(artifactUrl[prPrefix.Length..]);
            var parts = decoded.Split('/');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[^1], out int prId)) continue;

            var rawGuid = parts[^2].Trim('{', '}');
            repoMap.TryGetValue(rawGuid, out var repoInfo);
            var repoName = string.IsNullOrEmpty(repoInfo.RepoName) ? rawGuid : repoInfo.RepoName;
            var projectName = string.IsNullOrEmpty(repoInfo.ProjectName) ? _project : repoInfo.ProjectName;

            prLinks.Add(new PullRequestLink
            {
                PrId = prId,
                RepoName = repoName,
                RepoGuid = rawGuid,
                ProjectName = projectName,
                WebUrl = $"{_orgUrl}/{Uri.EscapeDataString(projectName)}/_git/{Uri.EscapeDataString(repoName)}/pullrequest/{prId}",
            });
        }

        // PR タイトルを並列取得
        await Task.WhenAll(prLinks.Select(pr => FetchPrTitleAsync(pr)));

        return prLinks;
    }

    private async Task FetchPrTitleAsync(PullRequestLink pr)
    {
        try
        {
            var res = await _httpClient.GetAsync(
                $"{_orgUrl}/{Uri.EscapeDataString(pr.ProjectName)}/_apis/git/repositories/{pr.RepoGuid}/pullrequests/{pr.PrId}?api-version=7.0");

            if (!res.IsSuccessStatusCode) return;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("title", out var titleEl))
                pr.Title = titleEl.GetString() ?? string.Empty;
        }
        catch { /* タイトル取得失敗は無視 */ }
        finally
        {
            pr.DisplayTitle = string.IsNullOrEmpty(pr.Title)
                ? $"PR #{pr.PrId} ({pr.RepoName})"
                : $"#{pr.PrId}: {pr.Title}";
        }
    }

    private static WorkItemData ParseWorkItem(JsonElement element)
    {
        var fields = element.GetProperty("fields");

        string GetStr(string field)
        {
            if (!fields.TryGetProperty(field, out var val)) return string.Empty;
            return val.ValueKind switch
            {
                JsonValueKind.String => val.GetString() ?? string.Empty,
                // System.AssignedTo は { displayName, id, ... } オブジェクト
                JsonValueKind.Object when val.TryGetProperty("displayName", out var dn)
                    => dn.GetString() ?? string.Empty,
                _ => string.Empty
            };
        }

        double? GetDouble(string field)
        {
            if (fields.TryGetProperty(field, out var val) && val.ValueKind == JsonValueKind.Number)
                return val.GetDouble();
            return null;
        }

        bool? GetBool(string field)
        {
            if (!fields.TryGetProperty(field, out var val)) return null;
            return val.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        return new WorkItemData
        {
            Id = element.GetProperty("id").GetInt32(),
            Title = GetStr("System.Title"),
            WorkItemType = GetStr("System.WorkItemType"),
            State = GetStr("System.State"),
            AssignedTo = GetStr("System.AssignedTo"),
            Activity = GetStr("Microsoft.VSTS.Common.Activity"),
            OriginalEstimate = GetDouble("Microsoft.VSTS.Scheduling.OriginalEstimate"),
            RemainingWork = GetDouble("Microsoft.VSTS.Scheduling.RemainingWork"),
            CompletedWork = GetDouble("Microsoft.VSTS.Scheduling.CompletedWork"),
            IsReview = GetBool("Custom.IsReview"),
            DevelopProcess = GetStr("Custom.DevelopProcess"),
        };
    }
}
