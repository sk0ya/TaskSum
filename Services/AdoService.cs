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
    /// ID リストを 200 件ずつバッチで取得します。
    /// </summary>
    public async Task<List<WorkItemData>> GetWorkItemsAsync(IEnumerable<int> ids)
    {
        var result = new List<WorkItemData>();
        var idList = ids.ToList();
        if (idList.Count == 0) return result;

        var fieldsParam = string.Join(",", WorkItemFields);

        for (int i = 0; i < idList.Count; i += 200)
        {
            var batch = idList.Skip(i).Take(200);
            var idsStr = string.Join(",", batch);

            var response = await _httpClient.GetAsync(
                $"{_orgUrl}/{_project}/_apis/wit/workitems" +
                $"?ids={idsStr}&fields={fieldsParam}&api-version=7.0");

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var value)) continue;

            foreach (var item in value.EnumerateArray())
                result.Add(ParseWorkItem(item));
        }

        return result;
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
        };
    }
}
