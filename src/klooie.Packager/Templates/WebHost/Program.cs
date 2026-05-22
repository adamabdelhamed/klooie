using klooie.blazor;
using klooie.blazor.Hosting;
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
        route: "__template_check",
        displayName: "Template Check",
        description: "Compile-time template validation entrypoint.",
        runAsync: () => Task.CompletedTask);
    return registry;
});

await builder.Build().RunAsync();
