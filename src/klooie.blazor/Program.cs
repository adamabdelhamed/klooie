using klooie.blazor;
using klooie.blazor.Hosting;
using klooie.blazorSampleApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(_ =>
{
    var registry = new KlooieBlazorAppRegistry();
    registry.Register(
        route: "DemoApp",
        displayName: "DemoApp",
        description: "Animated labels rendered through the browser console host.",
        runAsync: DemoAppProgram.MainAsync);
    return registry;
});

await builder.Build().RunAsync();
