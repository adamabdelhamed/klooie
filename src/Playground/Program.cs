using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using klooie;
using PowerArgs;
//BenchmarkRunner.Run<Benchmark>();
new Benchmark().CreateAppWithSomeControls();
[MemoryDiagnoser]
public class Benchmark
{
    [Benchmark]
    public void CreateAppWithSomeControls()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            while (true)
            {
                app.LayoutRoot.Add(new TextBox { Value = ConsoleString.Empty });
                app.LayoutRoot.Add(new Label { Text = ConsoleString.Empty });
                app.LayoutRoot.Add(new Button() { Text = ConsoleString.Empty });

                await Task.Yield();
                app.LayoutRoot.Controls.Clear();
                await Task.Yield();
            }
        });
        app.Run();
    }
}

