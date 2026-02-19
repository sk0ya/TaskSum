using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TaskSum.ViewModels;

namespace TaskSum;

public partial class MainWindow : Window
{
    private const int WM_MOUSEHWHEEL = 0x020E;

    // 集計パネルの行高さ推定値
    private const double AggPanelHeaderHeight = 32;
    private const double DataGridColumnHeaderHeight = 28;
    private const double DataGridRowHeight = 26;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        vm.AggregationItems.CollectionChanged += OnAggregationItemsChanged;
    }

    private void OnAggregationItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not System.Collections.ObjectModel.ObservableCollection<Models.AggregationItem> items)
            return;

        // ヘッダー + DataGrid列ヘッダー + 行数分の高さを計算
        double needed = AggPanelHeaderHeight + DataGridColumnHeaderHeight + items.Count * DataGridRowHeight;

        // ウィンドウ高さの 60% を上限とし、MinHeight(60) を下限とする
        double maxAllowed = Math.Max(60, ActualHeight * 0.6);
        double height = Math.Clamp(needed, 60, maxAllowed);

        RootGrid.RowDefinitions[4].Height = new GridLength(height);
    }

    private async void OnListViewItemContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListViewItem lvi) return;
        if (lvi.DataContext is not WorkItemNodeViewModel node) return;
        if (DataContext is not MainViewModel vm) return;

        var cm = lvi.ContextMenu;
        if (cm == null) return;

        // PRがまだ読み込まれていなければ非同期で取得してからメニューを開く
        if (!node.IsPrsLoaded)
        {
            e.Handled = true; // 一旦メニューを開かない
            await vm.LoadPullRequestsForNodeAsync(node);
            RebuildPrMenuItems(cm, node, vm);
            cm.PlacementTarget = lvi;
            cm.IsOpen = true;
            return;
        }

        RebuildPrMenuItems(cm, node, vm);
    }

    private static void RebuildPrMenuItems(ContextMenu cm, WorkItemNodeViewModel node, MainViewModel vm)
    {
        // 前回追加したPRアイテムをクリア（"Webで開く" の1件目は残す）
        while (cm.Items.Count > 1)
            cm.Items.RemoveAt(1);

        if (node.LinkedPullRequests.Count > 0)
        {
            cm.Items.Add(new Separator());
            foreach (var pr in node.LinkedPullRequests)
            {
                cm.Items.Add(new MenuItem
                {
                    Header = pr.DisplayTitle,
                    Command = vm.OpenPullRequestCommand,
                    CommandParameter = pr,
                });
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_MOUSEHWHEEL) return IntPtr.Zero;

        // 横スクロール量（正 = 右向き）
        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

        // カーソルのスクリーン座標
        int screenX = (short)(lParam.ToInt64() & 0xFFFF);
        int screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        var clientPoint = PointFromScreen(new Point(screenX, screenY));
        var hit = InputHitTest(clientPoint) as DependencyObject;

        // ビジュアルツリーを上に辿って ScrollViewer を探す
        var current = hit;
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset + delta / 3.0);
                handled = true;
                return IntPtr.Zero;
            }
            current = VisualTreeHelper.GetParent(current) as DependencyObject;
        }

        return IntPtr.Zero;
    }
}
