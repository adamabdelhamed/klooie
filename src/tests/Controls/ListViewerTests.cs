using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie.tests;
[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ListViewerTests
{
    public class Item
    {
        public string Foo { get; set; }
        public string Bar { get; set; }
    }

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ListViewer_Refresh() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05;
        var items = Enumerable.Range(0, 100).Select(i => new Item() { Bar = "Bar" + i, Foo = "Foo" + i }).ToList();
        var dataGrid = new ListViewer<Item>(new ListViewerOptions<Item>()
        {
            DataSource = items,
            Columns = new List<HeaderDefinition<Item>>()
            {
                new HeaderDefinition<Item>()
                {
                    Header = "Foo".ToGreen(),
                    Width = 20,
                    Type = GridValueType.Pixels,
                    Formatter = (item) => new Label(){ Text = item.Foo.ToConsoleString() }
                },
                new HeaderDefinition<Item>()
                {
                    Header = "Bar".ToRed(),
                    Width = 1,
                    Type = GridValueType.RemainderValue,
                    Formatter = (item) => new Label(){ Text = item.Bar.ToConsoleString() }
                }
            }
        });

        var selectionLabel = ConsoleApp.Current.LayoutRoot.Add(new Label() { Text = "DEFAULT".ToConsoleString(), Height = 1 }).CenterHorizontally();
        selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        dataGrid.SelectionChanged.Subscribe(() => selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString(), dataGrid);
        ConsoleApp.Current.LayoutRoot.Add(dataGrid).Fill(padding: new Thickness(0, 0, 1, 0));
        await context.PaintAndRecordKeyFrameAsync();

        for (var i = 0; i < 100; i++)
        {
            items[i].Foo = "FooRefreshed" + i;
            items[i].Bar = "BarRefreshed" + i;
        }

        dataGrid.Refresh();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void ListViewer_ProgrammaticSelection() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var items = Enumerable.Range(0, 100).Select(i => new Item() { Bar = "Bar" + i, Foo = "Foo" + i }).ToList();

        var dataGrid = new ListViewer<Item>(new ListViewerOptions<Item>()
        {
            DataSource = items,
            Columns = new List<HeaderDefinition<Item>>()
            {
                new HeaderDefinition<Item>()
                {
                    Header = "Foo".ToGreen(),
                    Width = 20,
                    Type = GridValueType.Pixels,
                    Formatter = (item) => new Label(){ Text = item.Foo.ToConsoleString() }
                },
                new HeaderDefinition<Item>()
                {
                    Header = "Bar".ToRed(),
                    Width = 20,
                    Type = GridValueType.Pixels,
                    Formatter = (item) => new Label(){ Text = item.Bar.ToConsoleString() }
                }
            },
        });

        var selectionLabel = ConsoleApp.Current.LayoutRoot.Add(new Label() { Text = "DEFAULT".ToConsoleString(), Height = 1 }).CenterHorizontally();
        selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        dataGrid.SelectionChanged.Subscribe(() =>
        {
            selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        }, dataGrid);
        ConsoleApp.Current.LayoutRoot.Add(dataGrid).Fill(padding: new Thickness(0, 0, 1, 0));
        await context.PaintAndRecordKeyFrameAsync();
        dataGrid.SelectedRowIndex = 1;
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();

    });

    [TestMethod]
    public void ListViewer_ProgrammaticSelectionForOffPageItem() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var items = Enumerable.Range(0, 100).Select(i => new Item() { Bar = "Bar" + i, Foo = "Foo" + i }).ToList();

        var dataGrid = new ListViewer<Item>(new ListViewerOptions<Item>()
        {
            DataSource = items,
            Columns = new List<HeaderDefinition<Item>>()
            {
                new HeaderDefinition<Item>()
                {
                    Header = "Foo".ToGreen(),
                    Width = 20,
                    Type = GridValueType.Pixels,
                    Formatter = (item) => new Label(){ Text = item.Foo.ToConsoleString() }
                },
                new HeaderDefinition<Item>()
                {
                    Header = "Bar".ToRed(),
                    Width = 20,
                    Type = GridValueType.Pixels,
                    Formatter = (item) => new Label(){ Text = item.Bar.ToConsoleString() }
                }
            },
        });

        var selectionLabel = ConsoleApp.Current.LayoutRoot.Add(new Label() { Text = "DEFAULT".ToConsoleString(), Height = 1 }).CenterHorizontally();
        selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        dataGrid.SelectionChanged.Subscribe(() =>
        {
            selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        }, dataGrid);
        ConsoleApp.Current.LayoutRoot.Add(dataGrid).Fill(padding: new Thickness(0, 0, 1, 0));
        await context.PaintAndRecordKeyFrameAsync();
        dataGrid.SelectedRowIndex = 1;
        dataGrid.SelectedRowIndex = 50;
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();

    });

    [TestMethod]
    public void ListViewer_CellSelection() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var items = Enumerable.Range(0, 100).Select(i => new Item() { Bar = "Bar" + i, Foo = "Foo" + i }).ToList();

        var dataGrid = new ListViewer<Item>(new ListViewerOptions<Item>()
        {
            DataSource = items,
            SelectionMode = ListViewerSelectionMode.Cell,
            Columns = new List<HeaderDefinition<Item>>()
                {
                    new HeaderDefinition<Item>()
                    {
                        Header = "Foo".ToGreen(),
                        Width = 20,
                        Type = GridValueType.Pixels,
                        Formatter = (item) => new Label(){ Text = item.Foo.ToConsoleString() }
                    },
                    new HeaderDefinition<Item>()
                    {
                        Header = "Bar".ToRed(),
                        Width = 20,
                        Type = GridValueType.Pixels,
                        Formatter = (item) => new Label(){ Text = item.Bar.ToConsoleString() }
                    }
                }
        });

        
        var selectionLabel = ConsoleApp.Current.LayoutRoot.Add(new Label() { Text = "DEFAULT".ToConsoleString(), Height = 1 }).CenterHorizontally();
        selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        dataGrid.SelectionChanged.Subscribe(() =>
        {
            selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
        }, dataGrid);
        ConsoleApp.Current.LayoutRoot.Add(dataGrid).Fill(padding: new Thickness(0, 0, 1, 0));
        dataGrid.Focus();
        await context.PaintAndRecordKeyFrameAsync();

        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.DownArrow, false, false, false));
        await context.PaintAndRecordKeyFrameAsync();


        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.RightArrow, false, false, false));
        await context.PaintAndRecordKeyFrameAsync();

        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.DownArrow, false, false, false));
        await context.PaintAndRecordKeyFrameAsync();

        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.LeftArrow, false, false, false));
        await context.PaintAndRecordKeyFrameAsync();

        ConsoleApp.Current.Stop();
    });
}
