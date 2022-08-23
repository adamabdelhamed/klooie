using klooie;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArgsTests.CLI.Controls
{
    [TestClass]
    [TestCategory(Categories.Slow)]
    public class ListGridTestsSlow
    {
        public class SlowList<T> : CachedRemoteList<T> where T : class
        {
            private List<T> items;
            public SlowList(List<T> items)
            {
                this.items = items;
            }

            protected override Task<int> FetchCountAsync()
            {
                var d = new TaskCompletionSource<int>();
                new Thread(() =>
                {
                    Thread.Sleep(50);
                    d.SetResult(items.Count);
                }).Start();
                return d.Task;
            }

            protected override Task<List<T>> FetchRangeAsync(int min, int count)
            {
                var d = new TaskCompletionSource<List<T>>();
                new Thread(() =>
                {
                    Thread.Sleep(50);
                    d.SetResult(items.Skip(min).Take(count).ToList());
                }).Start();
                return d.Task;
            }
        }


        public class Item
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestDataGridBasic()
        {
            var items = new List<Item>();

            for (var i = 0; i < 100; i++)
            {
                items.Add(new Item()
                {
                    Bar = "Bar" + i,
                    Foo = "Foo" + i,
                });
            }

            var app = new CliTestHarness(TestContext, 80, 20, true) { SecondsBetweenKeyframes = .05 };

            var dataGrid = new ListGrid<Item>(new ListGridOptions<Item>()
            {
                DataSource = new SyncList<Item>(items),
                Columns = new List<ListGridColumnDefinition<Item>>()
                {
                    new ListGridColumnDefinition<Item>()
                    {
                        Header = "Foo".ToGreen(),
                        Width = .5,
                        Type = GridValueType.Percentage,
                        Formatter = (item) => new Label(){ Text = item.Foo.ToConsoleString() }
                    },
                    new ListGridColumnDefinition<Item>()
                    {
                        Header = "Bar".ToRed(),
                        Width = .5,
                        Type = GridValueType.Percentage,
                        Formatter = (item) => new Label(){ Text = item.Bar.ToConsoleString() }
                    }
                }
            });

            app.InvokeNextCycle(async () =>
            {
                var selectionLabel = app.LayoutRoot.Add(new Label() { Text = "DEFAULT".ToConsoleString(), Height = 1 }).CenterHorizontally();
                selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
                dataGrid.SelectionChanged.SubscribeForLifetime(() =>
                {
                    selectionLabel.Text = $"SelectedRowIndex: {dataGrid.SelectedRowIndex}, SelectedCellIndex: {dataGrid.SelectedColumnIndex}".ToConsoleString();
                }, dataGrid);
                app.LayoutRoot.Add(dataGrid).Fill(padding: new Thickness(0, 0, 1, 0));
                await app.PaintAndRecordKeyFrameAsync();

                for (var i = 0; i < items.Count - 1; i++)
                {
                    await app.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.DownArrow, false, false, false));
                    await app.PaintAndRecordKeyFrameAsync();
                }

                for (var i = 0; i < items.Count - 1; i++)
                {
                    await app.SendKey(new ConsoleKeyInfo(' ', ConsoleKey.UpArrow, false, false, false));
                    await app.PaintAndRecordKeyFrameAsync();
                }
                app.Stop();
            });

            app.Start().Wait();
            app.AssertThisTestMatchesLKG();
        }
    }
}
