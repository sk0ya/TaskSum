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
    private bool _isLoading;
    private string _statusMessage = "準備完了";
    private bool _isAssignedToOpen;
    private bool _isStateOpen;
    private bool _isIsReviewOpen;
    private bool _isDevelopProcessOpen;
    private bool _isAggColumnsOpen;

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

    public bool IsAssignedToOpen
    {
        get => _isAssignedToOpen;
        set { _isAssignedToOpen = value; OnPropertyChanged(); }
    }

    public bool IsStateOpen
    {
        get => _isStateOpen;
        set { _isStateOpen = value; OnPropertyChanged(); }
    }

    public bool IsIsReviewOpen
    {
        get => _isIsReviewOpen;
        set { _isIsReviewOpen = value; OnPropertyChanged(); }
    }

    public bool IsDevelopProcessOpen
    {
        get => _isDevelopProcessOpen;
        set { _isDevelopProcessOpen = value; OnPropertyChanged(); }
    }

    public bool IsAggColumnsOpen
    {
        get => _isAggColumnsOpen;
        set { _isAggColumnsOpen = value; OnPropertyChanged(); }
    }

    public string AssignedToDisplayText
    {
        get
        {
            var checked_ = AssignedToOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            return checked_.Count == 0 ? "すべて" : string.Join(", ", checked_);
        }
    }

    public string StateDisplayText
    {
        get
        {
            var checked_ = StateOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            return checked_.Count == 0 ? "すべて" : string.Join(", ", checked_);
        }
    }

    public string IsReviewDisplayText
    {
        get
        {
            var checked_ = IsReviewOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            return checked_.Count == 0 ? "すべて" : string.Join(", ", checked_);
        }
    }

    public string DevelopProcessDisplayText
    {
        get
        {
            var checked_ = DevelopProcessOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            return checked_.Count == 0 ? "すべて" : string.Join(", ", checked_);
        }
    }

    // ----------------------------------------
    // 集計列 Visibility プロパティ
    // ----------------------------------------
    private Visibility ColVis(string key)
        => AggColumnOptions.FirstOrDefault(o => o.Key == key)?.IsChecked == true
           ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColCountVisibility      => ColVis("Count");
    public Visibility ColEstAllVisibility     => ColVis("EstAll");
    public Visibility ColEstReviewVisibility  => ColVis("EstReview");
    public Visibility ColEstNonReviewVisibility => ColVis("EstNonReview");
    public Visibility ColRemAllVisibility     => ColVis("RemAll");
    public Visibility ColRemReviewVisibility  => ColVis("RemReview");
    public Visibility ColRemNonReviewVisibility => ColVis("RemNonReview");
    public Visibility ColCmpAllVisibility     => ColVis("CmpAll");
    public Visibility ColCmpReviewVisibility  => ColVis("CmpReview");
    public Visibility ColCmpNonReviewVisibility => ColVis("CmpNonReview");
    public Visibility ColCmpRatioVisibility     => ColVis("CmpRatio");

    // ----------------------------------------
    // コレクション
    // ----------------------------------------
    public ObservableCollection<WorkItemNodeViewModel> VisibleNodes { get; } = [];
    public ObservableCollection<FilterOption> AssignedToOptions { get; } = [];
    public ObservableCollection<FilterOption> StateOptions { get; } = [];
    public ObservableCollection<FilterOption> IsReviewOptions { get; } = [];
    public ObservableCollection<FilterOption> DevelopProcessOptions { get; } = [];
    public ObservableCollection<AggregationItem> AggregationItems { get; } = [];
    public ObservableCollection<AggColumnOption> AggColumnOptions { get; } = [];

    // ----------------------------------------
    // コマンド
    // ----------------------------------------
    public ICommand LoadCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    // ----------------------------------------
    // コンストラクタ
    // ----------------------------------------
    public MainViewModel()
    {
        var settings = SettingsService.Load();
        OrganizationUrl = settings.OrganizationUrl;
        Project = settings.Project;

        InitAggColumnOptions(settings.HiddenAggregationColumns);

        LoadCommand = new AsyncRelayCommand(LoadWorkItemsAsync, () => !IsLoading);
        ToggleExpandCommand = new RelayCommand<WorkItemNodeViewModel>(ToggleExpand);
        ExpandAllCommand = new RelayCommand(() => SetExpandAll(true), () => !IsLoading);
        CollapseAllCommand = new RelayCommand(() => SetExpandAll(false), () => !IsLoading);
        ClearFiltersCommand = new RelayCommand(ClearAllFilters, () => !IsLoading);
    }

    // ----------------------------------------
    // 集計列設定
    // ----------------------------------------
    private void InitAggColumnOptions(List<string> hiddenColumns)
    {
        var defs = new[]
        {
            ("Count",        "件数"),
            ("EstAll",       "見積(全体)"),
            ("EstReview",    "見積(レビュー)"),
            ("EstNonReview", "見積(作業時間)"),
            ("RemAll",       "残余(全体)"),
            ("RemReview",    "残余(レビュー)"),
            ("RemNonReview", "残余(作業時間)"),
            ("CmpAll",       "完了(全体)"),
            ("CmpReview",    "完了(レビュー)"),
            ("CmpNonReview", "完了(作業時間)"),
            ("CmpRatio",     "完了レビュー率"),
        };

        foreach (var (key, displayName) in defs)
        {
            var opt = new AggColumnOption(key, displayName, !hiddenColumns.Contains(key));
            opt.PropertyChanged += OnAggColumnOptionChanged;
            AggColumnOptions.Add(opt);
        }
    }

    private void OnAggColumnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AggColumnOption.IsChecked)) return;
        if (sender is not AggColumnOption changed) return;

        OnPropertyChanged($"Col{changed.Key}Visibility");

        var hiddenColumns = AggColumnOptions.Where(o => !o.IsChecked).Select(o => o.Key).ToList();
        var settings = SettingsService.Load();
        settings.HiddenAggregationColumns = hiddenColumns;
        SettingsService.Save(settings);
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

        var settings = SettingsService.Load();
        settings.OrganizationUrl = OrganizationUrl;
        settings.Project = Project;
        SettingsService.Save(settings);

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
            {
                var node = new WorkItemNodeViewModel(wi);
                node.PropertyChanged += OnNodeCheckedChanged;
                nodeMap[wi.Id] = node;
            }

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

    private void ClearAllFilters()
    {
        foreach (var opt in AssignedToOptions) opt.IsChecked = false;
        foreach (var opt in StateOptions) opt.IsChecked = false;
        foreach (var opt in IsReviewOptions) opt.IsChecked = false;
        foreach (var opt in DevelopProcessOptions) opt.IsChecked = false;
        foreach (var node in GetAllNodes()) node.IsChecked = false;
    }

    // ----------------------------------------
    // フィルタ選択肢の更新
    // ----------------------------------------
    private void UpdateFilterOptions()
    {
        var allNodes = GetAllNodes().ToList();

        var prevCheckedAssigned = AssignedToOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        AssignedToOptions.Clear();
        foreach (var name in allNodes
            .Select(n => n.AssignedTo)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s))
        {
            var opt = new FilterOption(name, prevCheckedAssigned.Contains(name));
            opt.PropertyChanged += OnAssignedToOptionChanged;
            AssignedToOptions.Add(opt);
        }
        OnPropertyChanged(nameof(AssignedToDisplayText));

        var prevCheckedStates = StateOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        StateOptions.Clear();
        foreach (var state in allNodes
            .Select(n => n.State)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s))
        {
            var opt = new FilterOption(state, prevCheckedStates.Contains(state));
            opt.PropertyChanged += OnStateOptionChanged;
            StateOptions.Add(opt);
        }
        OnPropertyChanged(nameof(StateDisplayText));

        // IsReview: True / False の固定2択
        var prevCheckedIsReview = IsReviewOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        IsReviewOptions.Clear();
        foreach (var name in new[] { "True", "False" })
        {
            var opt = new FilterOption(name, prevCheckedIsReview.Contains(name));
            opt.PropertyChanged += OnIsReviewOptionChanged;
            IsReviewOptions.Add(opt);
        }
        OnPropertyChanged(nameof(IsReviewDisplayText));

        // DevelopProcess: データから動的生成
        var prevCheckedDevProcess = DevelopProcessOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        DevelopProcessOptions.Clear();
        foreach (var dp in allNodes
            .Select(n => n.DevelopProcess)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s))
        {
            var opt = new FilterOption(dp, prevCheckedDevProcess.Contains(dp));
            opt.PropertyChanged += OnDevelopProcessOptionChanged;
            DevelopProcessOptions.Add(opt);
        }
        OnPropertyChanged(nameof(DevelopProcessDisplayText));
    }

    private void OnAssignedToOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilterOption.IsChecked)) return;
        OnPropertyChanged(nameof(AssignedToDisplayText));
        ApplyFilters();
    }

    private void OnStateOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilterOption.IsChecked)) return;
        OnPropertyChanged(nameof(StateDisplayText));
        ApplyFilters();
    }

    private void OnIsReviewOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilterOption.IsChecked)) return;
        OnPropertyChanged(nameof(IsReviewDisplayText));
        ApplyFilters();
    }

    private void OnDevelopProcessOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilterOption.IsChecked)) return;
        OnPropertyChanged(nameof(DevelopProcessDisplayText));
        ApplyFilters();
    }

    private void OnNodeCheckedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkItemNodeViewModel.IsChecked)) return;
        var assignedSet = AssignedToOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var stateSet = StateOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var isReviewSet = IsReviewOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var devProcessSet = DevelopProcessOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        UpdateAggregation(assignedSet, stateSet, isReviewSet, devProcessSet);
    }

    // ----------------------------------------
    // フィルタ適用
    // ----------------------------------------
    private void ApplyFilters()
    {
        var assignedSet = AssignedToOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var stateSet = StateOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var isReviewSet = IsReviewOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();
        var devProcessSet = DevelopProcessOptions.Where(o => o.IsChecked).Select(o => o.Name).ToHashSet();

        VisibleNodes.Clear();
        foreach (var root in _rootNodes)
            AddVisibleNodes(root, assignedSet, stateSet, isReviewSet, devProcessSet);

        UpdateAggregation(assignedSet, stateSet, isReviewSet, devProcessSet);
    }

    private void AddVisibleNodes(WorkItemNodeViewModel node,
        HashSet<string> assignedSet, HashSet<string> stateSet,
        HashSet<string> isReviewSet, HashSet<string> devProcessSet)
    {
        bool selfMatches = MatchesFilter(node, assignedSet, stateSet, isReviewSet, devProcessSet);
        bool anyDescendantMatches = AnyDescendantMatches(node, assignedSet, stateSet, isReviewSet, devProcessSet);

        if (!selfMatches && !anyDescendantMatches) return;

        VisibleNodes.Add(node);

        if (!node.IsExpanded) return;

        foreach (var child in node.Children)
            AddVisibleNodes(child, assignedSet, stateSet, isReviewSet, devProcessSet);
    }

    private static bool AnyDescendantMatches(WorkItemNodeViewModel node,
        HashSet<string> assignedSet, HashSet<string> stateSet,
        HashSet<string> isReviewSet, HashSet<string> devProcessSet)
    {
        foreach (var child in node.Children)
        {
            if (MatchesFilter(child, assignedSet, stateSet, isReviewSet, devProcessSet) ||
                AnyDescendantMatches(child, assignedSet, stateSet, isReviewSet, devProcessSet))
                return true;
        }
        return false;
    }

    private static bool MatchesFilter(WorkItemNodeViewModel node,
        HashSet<string> assignedSet, HashSet<string> stateSet,
        HashSet<string> isReviewSet, HashSet<string> devProcessSet)
    {
        if (assignedSet.Count > 0 && !assignedSet.Contains(node.AssignedTo))
            return false;
        if (stateSet.Count > 0 && !stateSet.Contains(node.State))
            return false;
        if (isReviewSet.Count > 0)
        {
            var nodeReview = (node.IsReview ?? false).ToString(); // "True" or "False"
            if (!isReviewSet.Contains(nodeReview))
                return false;
        }
        if (devProcessSet.Count > 0 && !devProcessSet.Contains(node.DevelopProcess))
            return false;
        return true;
    }

    // ----------------------------------------
    // 集計
    // ----------------------------------------
    private void UpdateAggregation(HashSet<string> assignedSet, HashSet<string> stateSet,
        HashSet<string> isReviewSet, HashSet<string> devProcessSet)
    {
        AggregationItems.Clear();

        var checkedNodes = GetAllNodes().Where(n => n.IsChecked).ToList();
        List<WorkItemNodeViewModel> targets;
        if (checkedNodes.Count > 0)
        {
            var includedIds = new HashSet<int>();
            foreach (var node in checkedNodes)
                foreach (var n in Subtree(node))
                    includedIds.Add(n.Id);
            targets = GetAllNodes()
                .Where(n => includedIds.Contains(n.Id) &&
                            (n.OriginalEstimate.HasValue || n.RemainingWork.HasValue || n.CompletedWork.HasValue))
                .ToList();
        }
        else
        {
            targets = GetAllNodes()
                .Where(n => MatchesFilter(n, assignedSet, stateSet, isReviewSet, devProcessSet) &&
                            (n.OriginalEstimate.HasValue || n.RemainingWork.HasValue || n.CompletedWork.HasValue))
                .ToList();
        }

        if (targets.Count == 0) return;

        foreach (var group in targets
            .GroupBy(n => string.IsNullOrEmpty(n.DevelopProcess) ? "未設定" : n.DevelopProcess)
            .OrderBy(g => g.Key))
        {
            var reviewNodes    = group.Where(n => n.IsReview == true).ToList();
            var nonReviewNodes = group.Where(n => n.IsReview != true).ToList();

            AggregationItems.Add(new AggregationItem
            {
                Activity = group.Key,
                Count = group.Count(),
                TotalOriginalEstimate = group.Sum(n => n.OriginalEstimate ?? 0),
                TotalRemainingWork    = group.Sum(n => n.RemainingWork ?? 0),
                TotalCompletedWork    = group.Sum(n => n.CompletedWork ?? 0),
                ReviewOriginalEstimate    = reviewNodes.Sum(n => n.OriginalEstimate ?? 0),
                ReviewRemainingWork       = reviewNodes.Sum(n => n.RemainingWork ?? 0),
                ReviewCompletedWork       = reviewNodes.Sum(n => n.CompletedWork ?? 0),
                NonReviewOriginalEstimate = nonReviewNodes.Sum(n => n.OriginalEstimate ?? 0),
                NonReviewRemainingWork    = nonReviewNodes.Sum(n => n.RemainingWork ?? 0),
                NonReviewCompletedWork    = nonReviewNodes.Sum(n => n.CompletedWork ?? 0),
                IsTotal = false,
            });
        }

        // 合計行
        var reviewAll    = targets.Where(n => n.IsReview == true).ToList();
        var nonReviewAll = targets.Where(n => n.IsReview != true).ToList();

        AggregationItems.Add(new AggregationItem
        {
            Activity = "合計",
            Count = targets.Count,
            TotalOriginalEstimate = targets.Sum(n => n.OriginalEstimate ?? 0),
            TotalRemainingWork    = targets.Sum(n => n.RemainingWork ?? 0),
            TotalCompletedWork    = targets.Sum(n => n.CompletedWork ?? 0),
            ReviewOriginalEstimate    = reviewAll.Sum(n => n.OriginalEstimate ?? 0),
            ReviewRemainingWork       = reviewAll.Sum(n => n.RemainingWork ?? 0),
            ReviewCompletedWork       = reviewAll.Sum(n => n.CompletedWork ?? 0),
            NonReviewOriginalEstimate = nonReviewAll.Sum(n => n.OriginalEstimate ?? 0),
            NonReviewRemainingWork    = nonReviewAll.Sum(n => n.RemainingWork ?? 0),
            NonReviewCompletedWork    = nonReviewAll.Sum(n => n.CompletedWork ?? 0),
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
