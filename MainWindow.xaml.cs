using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TaskSum.ViewModels;

namespace TaskSum;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsLoading)) return;
        if (_vm.IsLoading || _vm.VisibleNodes.Count == 0) return;

        // レンダリング完了後にカラム幅をオートフィット
        Dispatcher.BeginInvoke(DispatcherPriority.Background, AutoFitColumns);
    }

    private void AutoFitColumns()
    {
        if (TreeListView.View is not GridView gridView) return;

        // オートフィット対象カラムインデックス: 種別(1), 状態(3), 担当者(4), Dev Process(6)
        int[] indices = [1, 3, 4, 6];

        // 仮想化を一時無効化してすべての行を測定
        VirtualizingPanel.SetIsVirtualizing(TreeListView, false);
        TreeListView.UpdateLayout();

        foreach (int i in indices)
        {
            if (i < gridView.Columns.Count)
                gridView.Columns[i].Width = 0;
        }
        TreeListView.UpdateLayout();

        foreach (int i in indices)
        {
            if (i < gridView.Columns.Count)
                gridView.Columns[i].Width = double.NaN;
        }
        TreeListView.UpdateLayout();

        // 仮想化を再有効化
        VirtualizingPanel.SetIsVirtualizing(TreeListView, true);
    }
}
