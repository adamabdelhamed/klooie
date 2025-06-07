namespace klooie;

public class ListViewerOptions<T> where T : class
{
    public bool ShowColumnHeaders { get; set; } = true;
    public bool ShowPager { get; set; } = true;
    public bool EnablePagerKeyboardShortcuts { get; set; } = true;
    public ListViewerSelectionMode SelectionMode { get; set; } = ListViewerSelectionMode.Row;
    public List<HeaderDefinition<T>> Columns { get; set; }
    public IList<T> DataSource { get; set; }
}

public enum ListViewerSelectionMode
{
    None,
    Row,
    Cell
}

public class HeaderDefinition : GridColumnDefinition
{
    public ConsoleString Header { get; set; }

}

public class HeaderDefinition<T> : HeaderDefinition where T : class
{ 
    public Func<T, ConsoleControl> Formatter { get; set; }
}

public partial class ListViewer<T> : ProtectedConsolePanel where T : class
{
    private ListViewerOptions<T> options;
    private int lastTopOfPageIndex;
    private int topOfPageDataIndex = 0;

    private ListViewerPanel presenter;
    private List<ConsoleControl> highlightedControls;
    private List<Recyclable> highlightLifetimes = new List<Recyclable>();

    private Event selectionChanged;
    public Event SelectionChanged => selectionChanged ??= Event.Create();
    public partial int SelectedRowIndex { get; set; }
    public partial int SelectedColumnIndex { get; set; }
    public int PageIndex => (int)Math.Floor(topOfPageDataIndex / (double)presenter.MaxRowsThatCanBePresented);
    public int PageCount => (int)Math.Ceiling(options.DataSource.Count / (double)presenter.MaxRowsThatCanBePresented);

    public ListViewer(ListViewerOptions<T> options)
    {
        this.options = options;
        highlightedControls = new List<ConsoleControl>();
        CanFocus = options.SelectionMode != ListViewerSelectionMode.None;
        Focused.Subscribe(UpdateHighlightedRowsToReflectCurrentFocus, this);
        Unfocused.Subscribe(UpdateHighlightedRowsToReflectCurrentFocus, this);
        KeyInputReceived.Subscribe(HandleArrows, this);

        presenter = ProtectedPanel.Add(new ListViewerPanel(new ListViewerPanelOptions()
        {
            Columns = options.Columns.Cast<HeaderDefinition>().ToList(),
            ShowColumnHeaders = options.ShowColumnHeaders,
            ShowPager = options.ShowPager,
            EnablePagerKeyboardShortcuts = options.EnablePagerKeyboardShortcuts,
        })).Fill();

        presenter.BeforeRecompose.Subscribe(BeforeRecompose, this);
        presenter.AfterRecompose.Subscribe(UpdateHighlightedRowsToReflectCurrentFocus, this);
        presenter.FirstPageClicked.Subscribe(FirstPageClicked, this);
        presenter.PreviousPageClicked.Subscribe(PreviousPageClicked, this);
        presenter.NextPageClicked.Subscribe(NextPageClicked, this);
        presenter.LastPageClicked.Subscribe(LastPageClicked, this);

        SelectedRowIndexChanged.Subscribe(SelectedRowChanged, this);
        SelectedColumnIndexChanged.Subscribe(SelectedColumnChanged, this);
    }

    /// <summary>
    /// We could just call presenter.Recompose() when the selected row changes, but it would be slow.
    /// Instead, we only do this if this selection change caused a page change. In the cases when the 
    /// selection change is within the current page we will just change the highlighted controls, which
    /// is much more performant.
    /// </summary>
    private void SelectedRowChanged()
    {
        // Calculate the index of the row in the current page.
        var presentedRowIndex = SelectedRowIndex - topOfPageDataIndex;

        // If the presenter hasn't been composed yet or the computed index isn't available,
        // force a recompose so that ControlsByRow is populated.
        if (presenter.ControlsByRow == null || !presenter.ControlsByRow.ContainsKey(presentedRowIndex))
        {
            presenter.Recompose();
            lastTopOfPageIndex = topOfPageDataIndex;
            SelectedRowChanged();
            return;
        }

        // If the top-of-page index has changed, recompose.
        if (lastTopOfPageIndex != topOfPageDataIndex)
        {
            presenter.Recompose();
        }
        else
        {
            var rowControls = presenter.ControlsByRow[presentedRowIndex];
            highlightedControls.Clear();

            for (var i = 0; i < rowControls.Count; i++)
            {
                if (options.SelectionMode == ListViewerSelectionMode.Row || i == SelectedColumnIndex)
                {
                    highlightedControls.Add(rowControls[i]);
                }
            }

            Highlight(highlightedControls);
        }

        lastTopOfPageIndex = topOfPageDataIndex;
        SelectionChanged.Fire();
    }

    private void SelectedColumnChanged()
    {
        var rowControls = presenter.ControlsByRow[SelectedRowIndex - topOfPageDataIndex];
        highlightedControls.Clear();

        for (var i = 0; i < rowControls.Count; i++)
        {
            if (options.SelectionMode == ListViewerSelectionMode.Row || i == SelectedColumnIndex)
            {
                highlightedControls.Add(rowControls[i]);
            }
        }

        Highlight(highlightedControls);
        SelectionChanged.Fire();
    }

    public void Refresh() => presenter.Recompose();



    private void BeforeRecompose()
    {
        highlightedControls.Clear();

        if (options.SelectionMode != ListViewerSelectionMode.None)
        {
            int pageSize = presenter.MaxRowsThatCanBePresented;
            topOfPageDataIndex = (SelectedRowIndex / pageSize) * pageSize;
        }
        else
        {
            topOfPageDataIndex = 0;
        }

        var range = options.DataSource
                     .Skip(topOfPageDataIndex)
                     .Take(presenter.MaxRowsThatCanBePresented)
                     .ToArray();
        presenter.Options.Rows = new List<DataGridPresentationRow>();

        for (var i = 0; i < range.Length; i++)
        {
            var item = range[i];
            var deepIndex = i + topOfPageDataIndex;
            var row = new DataGridPresentationRow();
            presenter.Options.Rows.Add(row);
            for (var j = 0; j < options.Columns.Count; j++)
            {
                var col = options.Columns[j];

                bool shouldBeHighlighted = false;
                if (options.SelectionMode == ListViewerSelectionMode.Row && deepIndex == SelectedRowIndex)
                {
                    shouldBeHighlighted = true;
                }
                else if (options.SelectionMode == ListViewerSelectionMode.Cell && deepIndex == SelectedRowIndex && SelectedColumnIndex == j)
                {
                    shouldBeHighlighted = true;
                }

                row.Cells.Add(() =>
                {
                    var control = col.Formatter(item);
                    if (shouldBeHighlighted)
                    {
                        highlightedControls.Add(control);
                    }
                    return control;
                });
            }
        }

        presenter.Options.PagerState = new PagerState()
        {
            AllowRandomAccess = true,
            CanGoBackwards = PageIndex > 0,
            CanGoForwards = PageIndex < PageCount - 1,
            CurrentPageLabelValue = $"Page {PageIndex + 1} of {PageCount}".ToConsoleString(),
        };
    }

    private void UpdateHighlightedRowsToReflectCurrentFocus() => Highlight(highlightedControls);

    private void Highlight(List<ConsoleControl> controls)
    {
        while(highlightLifetimes.Count > 0)
        {
            var toDispose = highlightLifetimes[0];
            highlightLifetimes.RemoveAt(0);
            toDispose.TryDispose();
        }

        highlightLifetimes.Clear();

        foreach (var cellDisplayControl in controls)
        {
            var highlightLifetime = DefaultRecyclablePool.Instance.Rent();
            highlightLifetimes.Add(highlightLifetime);
            highlightLifetime.OnDisposed(() => highlightLifetimes.Remove(highlightLifetime));
            if (cellDisplayControl is Label label)
            {
                var originalText = label.Text;
                label.Text = label.Text.ToBlack().ToDifferentBackground(HasFocus ? RGB.Cyan : RGB.DarkGray);
                highlightLifetime.OnDisposed(() =>
                {
                    label.Text = originalText;
                    
                });
            }
            else
            {
                var originalFg = cellDisplayControl.Foreground;
                var originalBg = cellDisplayControl.Background;
                cellDisplayControl.Foreground = RGB.White;
                cellDisplayControl.Background = HasFocus ? RGB.Cyan : RGB.DarkGray;
                highlightLifetime.OnDisposed(() =>
                {
                    cellDisplayControl.Foreground = originalFg;
                    cellDisplayControl.Background = originalBg;    
                });
            }
        }
    }

    private void FirstPageClicked()
    {
        topOfPageDataIndex = 0;
        SelectedRowIndex = topOfPageDataIndex;
    }

    private void PreviousPageClicked()
    {
        topOfPageDataIndex = Math.Max(0, topOfPageDataIndex - presenter.MaxRowsThatCanBePresented);
        SelectedRowIndex = topOfPageDataIndex;
    }

    private void NextPageClicked()
    {
        topOfPageDataIndex = Math.Min(options.DataSource.Count - 1, topOfPageDataIndex + presenter.MaxRowsThatCanBePresented);
        SelectedRowIndex = topOfPageDataIndex;
    }

    private void LastPageClicked()
    {
        topOfPageDataIndex = (PageCount - 1) * presenter.MaxRowsThatCanBePresented;
        SelectedRowIndex = topOfPageDataIndex;
    }

    private void HandleArrows(ConsoleKeyInfo keyInfo)
    {
        if (options.SelectionMode != ListViewerSelectionMode.None)
        {
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (SelectedRowIndex > 0)
                {
                    if (SelectedRowIndex == topOfPageDataIndex)
                    {
                        topOfPageDataIndex = Math.Max(0, SelectedRowIndex - presenter.MaxRowsThatCanBePresented);

                    }

                    SelectedRowIndex--;
                }
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (SelectedRowIndex < options.DataSource.Count - 1)
                {
                    if (SelectedRowIndex == topOfPageDataIndex + presenter.MaxRowsThatCanBePresented - 1)
                    {
                        topOfPageDataIndex = SelectedRowIndex + 1;
                    }
                    SelectedRowIndex++;
                }
            }
        }

        if (options.SelectionMode == ListViewerSelectionMode.Cell)
        {
            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (SelectedColumnIndex > 0)
                {
                    SelectedColumnIndex--;
                }
            }
            else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (SelectedColumnIndex < options.Columns.Count - 1)
                {
                    SelectedColumnIndex++;
                }
            }
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        selectionChanged?.TryDispose();
        selectionChanged = null;
        while (highlightLifetimes.Count > 0)
        {
            var toDispose = highlightLifetimes[0];
            highlightLifetimes.RemoveAt(0);
            toDispose.TryDispose();
        }
        presenter?.TryDispose();
        presenter = null;
        highlightedControls?.Clear();
    }
}


internal class DataGridPresentationRow
{
    public List<Func<ConsoleControl>> Cells { get; set; } = new List<Func<ConsoleControl>>();
}

internal class PagerState
{
    public bool AllowRandomAccess { get; set; }
    public bool CanGoBackwards { get; set; }
    public bool CanGoForwards { get; set; }
    public ConsoleString CurrentPageLabelValue { get; set; }
}

internal class ListViewerPanelOptions
{
    public bool ShowColumnHeaders { get; set; } = true;
    public bool ShowPager { get; set; } = true;
    public bool EnablePagerKeyboardShortcuts { get; set; } = true;
    public List<HeaderDefinition> Columns { get; set; }
    public List<DataGridPresentationRow> Rows { get; set; }
    public PagerState PagerState { get; set; }
}

internal class ListViewerPanel : ProtectedConsolePanel
{
    private RandomAccessPager pager;
    private GridLayout gridLayout;
    private ConsolePanel pagerContainer;
    private GridLayoutOptions layoutOptions;
    private bool firstButtonFocused, previousButtonFocused, nextButtonFocused, lastButtonFocused;
    public ListViewerPanelOptions Options { get; private set; }
    public Dictionary<int, List<ConsoleControl>> ControlsByRow { get; private set; } = new Dictionary<int, List<ConsoleControl>>();
    private Event firstPageClicked;
    private Event previousPageClicked;
    private Event nextPageClicked;
    private Event lastPageClicked;
    private Event beforeRecompose;
    private Event afterRecompose;
    public Event FirstPageClicked => firstPageClicked ??= Event.Create();
    public Event PreviousPageClicked => previousPageClicked ??= Event.Create();
    public Event NextPageClicked => nextPageClicked ??= Event.Create();
    public Event LastPageClicked => lastPageClicked ??= Event.Create();
    public Event BeforeRecompose => beforeRecompose ??= Event.Create();
    public Event AfterRecompose => afterRecompose ??= Event.Create();
    public int MaxRowsThatCanBePresented => Options.ShowColumnHeaders ? Math.Max(0, Height - 2) : Math.Max(0, Height - 1);

    public ListViewerPanel(ListViewerPanelOptions options)
    {
        this.Options = options;
        layoutOptions = new GridLayoutOptions() { Columns = options.Columns.Cast<GridColumnDefinition>().ToList(), Rows = new List<GridRowDefinition>() };
        BoundsChanged.Subscribe(Recompose, this);
    }

    public void Recompose()
    {
        if (MaxRowsThatCanBePresented == 0) return;
        BeforeRecompose.Fire();
        SnapshotPagerFocus();
        DisposeGrid();
        ComposeGridLayout();
        ComposeDataCells();
        ComposePager();
        AfterRecompose.Fire();
    }

    private void SnapshotPagerFocus()
    {
        firstButtonFocused = pager != null && pager.FirstPageButton.HasFocus;
        previousButtonFocused = pager != null && pager.PreviousPageButton.HasFocus;
        nextButtonFocused = pager != null && pager.NextPageButton.HasFocus;
        lastButtonFocused = pager != null && pager.LastPageButton.HasFocus;
    }

    private void DisposeGrid()
    {
        gridLayout?.Dispose();
        ControlsByRow.Clear();
    }

    private void ComposeGridLayout()
    {
        layoutOptions.Rows = new List<GridRowDefinition>();
        for (var i = 0; i < Height; i++)
        {
            layoutOptions.Rows.Add(new GridRowDefinition() { Height = 1, Type = GridValueType.Pixels });
        }

        gridLayout = ProtectedPanel.Add(new GridLayout(layoutOptions.GetRowSpec(), layoutOptions.GetColumnSpec())).Fill();
    }
    private void ComposeDataCells()
    {
        if (Options.ShowColumnHeaders)
        {
            for (var col = 0; col < Options.Columns.Count; col++)
            {
                gridLayout.Add(new Label() { Text = Options.Columns[col].Header }, col, 0);
            }
        }

        var dataRowStartIndex = Options.ShowColumnHeaders ? 1 : 0;
        var currentIndex = 0;
        for (var gridLayoutRow = dataRowStartIndex; gridLayoutRow < dataRowStartIndex + MaxRowsThatCanBePresented; gridLayoutRow++)
        {
            if (currentIndex >= Options.Rows.Count) break;
            var dataItem = Options.Rows[currentIndex];
            var rowControls = new List<ConsoleControl>();
            ControlsByRow.Add(currentIndex, rowControls);
            for (var gridLayoutCol = 0; gridLayoutCol < Options.Columns.Count; gridLayoutCol++)
            {
                var columnDefinition = Options.Columns[gridLayoutCol];
                var cellDisplayControl = gridLayout.Add(dataItem.Cells[gridLayoutCol].Invoke(), gridLayoutCol, gridLayoutRow);
                rowControls.Add(cellDisplayControl);

            }
            currentIndex++;
        }
    }

    private void ComposePager()
    {
        pagerContainer = gridLayout.Add(new ConsolePanel(), 0, Height - 1, layoutOptions.Columns.Count, 1);
        pager = pagerContainer.Add(new RandomAccessPager(Options.EnablePagerKeyboardShortcuts)).CenterHorizontally();
        pager.IsVisible = Options.ShowPager;
        pager.FirstPageButton.Pressed.Subscribe(FirstPageClicked.Fire, pager);
        pager.PreviousPageButton.Pressed.Subscribe(PreviousPageClicked.Fire, pager);
        pager.NextPageButton.Pressed.Subscribe(NextPageClicked.Fire, pager);
        pager.LastPageButton.Pressed.Subscribe(LastPageClicked.Fire, pager);
        pager.FirstPageButton.CanFocus = Options.PagerState.CanGoBackwards;
        pager.PreviousPageButton.CanFocus = Options.PagerState.CanGoBackwards;
        pager.NextPageButton.CanFocus = Options.PagerState.CanGoForwards;
        pager.LastPageButton.CanFocus = Options.PagerState.CanGoForwards;
        pager.CurrentPageLabel.Text = Options.PagerState.CurrentPageLabelValue;
        if (Options.PagerState.AllowRandomAccess == false)
        {
            pager.Controls.Remove(pager.LastPageButton);
        }

        
        if (firstButtonFocused && FocusManager.CanReceiveFocusNow(pager.FirstPageButton)) pager.FirstPageButton.Focus();
        else if (previousButtonFocused && FocusManager.CanReceiveFocusNow(pager.PreviousPageButton)) pager.PreviousPageButton.Focus();
        else if (nextButtonFocused && FocusManager.CanReceiveFocusNow(pager.NextPageButton)) pager.NextPageButton.Focus();
        else if (lastButtonFocused && FocusManager.CanReceiveFocusNow(pager.LastPageButton)) pager.LastPageButton.Focus();

    }

    protected override void OnReturn()
    {
        base.OnReturn();
        firstPageClicked?.TryDispose();
        firstPageClicked = null;
        previousPageClicked?.TryDispose();
        previousPageClicked = null;
        nextPageClicked?.TryDispose();
        nextPageClicked = null;
        lastPageClicked?.TryDispose();
        lastPageClicked = null;
        beforeRecompose?.TryDispose();
        beforeRecompose = null;
        afterRecompose?.TryDispose();
        afterRecompose = null;
        pager?.TryDispose();
        pager = null;
        gridLayout?.TryDispose();
        gridLayout = null;
        pagerContainer?.TryDispose();
        pagerContainer = null;
        ControlsByRow.Clear();
    }

    private class RandomAccessPager : StackPanel
    {
        public Button FirstPageButton { get; private set; }
        public Button PreviousPageButton { get; private set; }
        public Label CurrentPageLabel { get; private set; }
        public Button NextPageButton { get; private set; }
        public Button LastPageButton { get; private set; }

        public RandomAccessPager(bool enableShortcuts)
        {
            AutoSize = AutoSizeMode.Both;
            Margin = 2;
            Orientation = Orientation.Horizontal;
            FirstPageButton = Add(new Button(enableShortcuts ? new KeyboardShortcut(ConsoleKey.Home) : null) { Text = "<<".ToConsoleString() });
            PreviousPageButton = Add(new Button(enableShortcuts ? new KeyboardShortcut(ConsoleKey.PageUp) : null) { Text = "<".ToConsoleString() });
            CurrentPageLabel = Add(new Label() { Text = "Page 1 of 1".ToConsoleString() });
            NextPageButton = Add(new Button(enableShortcuts ? new KeyboardShortcut(ConsoleKey.PageDown) : null) { Text = ">".ToConsoleString() });
            LastPageButton = Add(new Button(enableShortcuts ? new KeyboardShortcut(ConsoleKey.End) : null) { Text = ">>".ToConsoleString() });
        }
    }
}
