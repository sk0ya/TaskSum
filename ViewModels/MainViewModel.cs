using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using TaskSum.Commands;
using TaskSum.Models;
using TaskSum.Services;

namespace TaskSum.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // ----------------------------------------
    // フィールド
    // ----------------------------------------
    private string _organizationUrl = string.Empty;
    private string _project = string.Empty;
    private string _featureId = string.Empty;
    private string _assignedToFilter = "All";
    private string _stateFilter = "All";
    private bool _isLoading;
    private string _statusMessage = "準備完了";

    private readonly List<WorkItemNodeViewModel> _rootNodes = [];

    // ----------------------------------------
    // プロパティ
    // ----------------------------------------
    public string OrganizationUrl
    {
        get => _organizationUrl;
        set { _organizationUrl = value; OnPropertyChanged(); }
    }

    public string Project
    {
        get => _project;
        set { _project = value; OnPropertyChanged(); }
    }

    public string FeatureId
    {
        get => _featureId;
        set { _featureId = value; OnPropertyChanged(); }
    }

    public string AssignedToFilter
    {
        get => _assignedToFilter;
        set
        {
            if (_assignedToFilter == value) return;
            _assignedToFilter = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public string StateFilter
    {
        get => _stateFilter;
        set
        {
            if (_stateFilter == value) return;
            _stateFilter = value;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            Application.Current?.Dispatcher.Invoke(
                System.Windows.Input.CommandManager.InvalidateRequerySuggested);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    // ----------------------------------------
    // コレクション
    // ----------------------------------------
    public ObservableCollection<WorkItemNodeViewModel> VisibleNodes { get; } = [];
    public ObservableCollection<string> AssignedToOptions { get; } = [];
    public ObservableCollection<string> StateOptions { get; } = [];
    public ObservableCollection<AggregationItem> AggregationItems { get; } = [];

    // ----------------------------------------
    // コマンド
    // ----------------------------------------
    public ICommand LoadCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }

    // ----------------------------------------
    // コンストラクタ
    // ----------------------------------------
    public MainViewModel()
    {
        var settings = SettingsService.Load();
        OrganizationUrl = settings.OrganizationUrl;
        Project = settings.Project;

        LoadCommand = new AsyncRelayCommand(LoadWorkItemsAsync, () => !IsLoading);
        ToggleExpandCommand = new RelayCommand<WorkItemNodeViewModel>(ToggleExpand);
        ExpandAllCommand = new RelayCommand(() => SetExpandAll(true), () => !IsLoading);
        CollapseAllCommand = new RelayCommand(() => SetExpandAll(false), () => !IsLoading);

        AssignedToOptions.Add("All");
        StateOptions.Add("All");
    }

    // ----------------------------------------
    // 読み込み
    // ----------------------------------------
    private async Task LoadWorkItemsAsync()
    {
        if (!int.TryParse(FeatureId.Trim(), out int featureId))
        {
            StatusMessage = "有効なフィーチャ ID を入力してください。";
            return;
        }
        if (string.IsNullOrWhiteSpace(OrganizationUrl) || string.IsNullOrWhiteSpace(Project))
        {
            StatusMessage = "Organization URL と Project を入力してください。";
            return;
        }

        SettingsService.Save(new AppSettings
        {
            OrganizationUrl = OrganizationUrl,
            Project = Project,
        });

        IsLoading = true;
        _rootNodes.Clear();
        VisibleNodes.Clear();
        AggregationItems.Clear();

        try
        {
            // PAT 取得
            StatusMessage = "PAT を取得中...";
            var pat = CredentialManagerService.GetPat();
            if (pat == null)
            {
                StatusMessage = "エラー: Windows 資格情報マネージャーに 'ADO_PAT' が見つかりません。";
                return;
            }

            var service = new AdoService(OrganizationUrl.Trim(), Project.Trim(), pat);

            // リンク取得 (WIQL Recursive)
            StatusMessage = "子アイテムのリンクを取得中...";
            var links = await service.GetDescendantLinksAsync(featureId);

            if (links.Count == 0)
            {
                StatusMessage = "アイテムが見つかりませんでした。Feature ID を確認してください。";
                return;
            }

            // 親子マップと全 ID セットを構築
            var parentMap = new Dictionary<int, int>(); // childId -> parentId (0 = root)
            var allIds = new HashSet<int>();
            foreach (var (srcId, tgtId) in links)
            {
                parentMap[tgtId] = srcId;
                allIds.Add(tgtId);
            }

            // バッチでワークアイテム取得
            StatusMessage = $"{allIds.Count} 件のアイテムを取得中...";
            var workItems = await service.GetWorkItemsAsync(allIds);

            // ノードマップ構築
            var nodeMap = new Dictionary<int, WorkItemNodeViewModel>(workItems.Count);
            foreach (var wi in workItems)
                nodeMap[wi.Id] = new WorkItemNodeViewModel(wi);

            // 親子関係を確立
            foreach (var (childId, parentId) in parentMap)
            {
                if (!nodeMap.TryGetValue(childId, out var childNode)) continue;

                if (parentId > 0 && nodeMap.TryGetValue(parentId, out var parentNode))
                {
                    parentNode.Children.Add(childNode);
                    childNode.Parent = parentNode;
                }
                else
                {
                    _rootNodes.Add(childNode); // root (Feature自身)
                }
            }

            // ID 順にソート
            _rootNodes.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var node in GetAllNodes())
                node.Children.Sort((a, b) => a.Id.CompareTo(b.Id));

            // レベル設定
            foreach (var root in _rootNodes)
                SetLevel(root, 0);

            // フィルタ選択肢を更新
            UpdateFilterOptions();

            // 表示に反映
            ApplyFilters();

            StatusMessage = $"完了: {allIds.Count} 件を読み込みました。";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"API エラー: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ----------------------------------------
    // ツリー操作
    // ----------------------------------------
    private void ToggleExpand(WorkItemNodeViewModel? node)
    {
        if (node == null) return;
        node.IsExpanded = !node.IsExpanded;
        ApplyFilters();
    }

    private void SetExpandAll(bool expanded)
    {
        foreach (var node in GetAllNodes())
            node.IsExpanded = expanded;
        ApplyFilters();
    }

    private static void SetLevel(WorkItemNodeViewModel node, int level)
    {
        node.Level = level;
        foreach (var child in node.Children)
            SetLevel(child, level + 1);
    }

    // ----------------------------------------
    // フィルタ
    // ----------------------------------------
    private void UpdateFilterOptions()
    {
        var allNodes = GetAllNodes().ToList();

        var prevAssigned = AssignedToFilter;
        AssignedToOptions.Clear();
        AssignedToOptions.Add("All");
        foreach (var name in allNodes
            .Select(n => n.AssignedTo)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s))
        {
            AssignedToOptions.Add(name);
        }
        _assignedToFilter = AssignedToOptions.Contains(prevAssigned) ? prevAssigned : "All";
        OnPropertyChanged(nameof(AssignedToFilter));

        var prevState = StateFilter;
        StateOptions.Clear();
        StateOptions.Add("All");
        foreach (var state in allNodes
            .Select(n => n.State)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s))
        {
            StateOptions.Add(state);
        }
        _stateFilter = StateOptions.Contains(prevState) ? prevState : "All";
        OnPropertyChanged(nameof(StateFilter));
    }

    /// <summary>
    /// フィルタ条件で VisibleNodes を再構築します。
    /// 条件に合致するアイテムとその祖先を表示します。
    /// </summary>
    private void ApplyFilters()
    {
        VisibleNodes.Clear();
        foreach (var root in _rootNodes)
            AddVisibleNodes(root);
        UpdateAggregation();
    }

    private void AddVisibleNodes(WorkItemNodeViewModel node)
    {
        bool selfMatches = MatchesFilter(node);
        bool anyDescendantMatches = AnyDescendantMatches(node);

        if (!selfMatches && !anyDescendantMatches) return;

        VisibleNodes.Add(node);

        if (!node.IsExpanded) return;

        foreach (var child in node.Children)
            AddVisibleNodes(child);
    }

    private bool AnyDescendantMatches(WorkItemNodeViewModel node)
    {
        foreach (var child in node.Children)
        {
            if (MatchesFilter(child) || AnyDescendantMatches(child))
                return true;
        }
        return false;
    }

    private bool MatchesFilter(WorkItemNodeViewModel node)
    {
        if (AssignedToFilter != "All" && node.AssignedTo != AssignedToFilter)
            return false;
        if (StateFilter != "All" && node.State != StateFilter)
            return false;
        return true;
    }

    // ----------------------------------------
    // 集計
    // ----------------------------------------
    private void UpdateAggregation()
    {
        AggregationItems.Clear();

        // フィルタ条件に合致し、工数フィールドを持つ VisibleNode のみ集計
        var targets = VisibleNodes
            .Where(n => MatchesFilter(n) &&
                        (n.OriginalEstimate.HasValue || n.RemainingWork.HasValue || n.CompletedWork.HasValue))
            .ToList();

        if (targets.Count == 0) return;

        foreach (var group in targets
            .GroupBy(n => string.IsNullOrEmpty(n.Activity) ? "未設定" : n.Activity)
            .OrderBy(g => g.Key))
        {
            AggregationItems.Add(new AggregationItem
            {
                Activity = group.Key,
                Count = group.Count(),
                TotalOriginalEstimate = group.Sum(n => n.OriginalEstimate ?? 0),
                TotalRemainingWork = group.Sum(n => n.RemainingWork ?? 0),
                TotalCompletedWork = group.Sum(n => n.CompletedWork ?? 0),
                IsTotal = false,
            });
        }

        // 合計行
        AggregationItems.Add(new AggregationItem
        {
            Activity = "合計",
            Count = targets.Count,
            TotalOriginalEstimate = targets.Sum(n => n.OriginalEstimate ?? 0),
            TotalRemainingWork = targets.Sum(n => n.RemainingWork ?? 0),
            TotalCompletedWork = targets.Sum(n => n.CompletedWork ?? 0),
            IsTotal = true,
        });
    }

    // ----------------------------------------
    // ヘルパー
    // ----------------------------------------
    private IEnumerable<WorkItemNodeViewModel> GetAllNodes()
    {
        foreach (var root in _rootNodes)
            foreach (var node in Subtree(root))
                yield return node;
    }

    private static IEnumerable<WorkItemNodeViewModel> Subtree(WorkItemNodeViewModel node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var n in Subtree(child))
                yield return n;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
