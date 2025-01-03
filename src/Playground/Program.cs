using klooie;
using klooie.Gaming;
using PowerArgs;
var app = new Game();

long initialMemory = 0;
long currentMemory = 0;
app.Invoke(async () =>
{
    app.PaintEnabled = false;
    for (var i = 0; i < 10; i++)
    {
        var control = ConsolePanelPool.Instance.Rent();
        control.Background = RGB.Red;
        ConsoleApp.Current.LayoutRoot.Add(control);
        await Task.Yield();
        ConsolePanelPool.Instance.Return(control);

        if (i < 2)
        {
            // Run the garbage collector
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Store the amount of memory currently allocated on the heap
            initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        }
        else
        {
            // Validate that there are no allocations
            currentMemory = GC.GetTotalMemory(forceFullCollection: true);

            // Assert that the current memory is not greater than the initial memory
           if(currentMemory > initialMemory)
            {
                $"Memory allocation occurred at iteration {i}. Initial: {initialMemory}, Current: {currentMemory}. It grew by {(currentMemory - initialMemory) / 1000} kb".ToRed().WriteLine();
            }
        }
    }
    app.Stop();
});


app.Run();

Console.WriteLine($"Memory allocation. Initial: {initialMemory}, Current: {currentMemory}");