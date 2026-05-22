# klooie.Packager

`klooie.Packager` builds the `kpack` executable. Use it to package a klooie project as either a Windows executable or a static Blazor WebAssembly web app.

## Build kpack

```powershell
dotnet build .\src\klooie.Packager\klooie.Packager.csproj
```

The debug build creates:

```text
src\klooie.Packager\bin\Debug\net10.0\kpack.exe
```

You can also run it through `dotnet run` while developing:

```powershell
dotnet run --project .\src\klooie.Packager\klooie.Packager.csproj -- <project.csproj> -type Web
```

## Web Packaging

```powershell
kpack SomeCSProj.csproj -type Web
```

This creates:

```text
SomeProject\bin\klooie.web
```

That folder is the final static web output. Publish the contents of `bin\klooie.web` to a static web server. It contains `index.html`, `_framework`, CSS, JavaScript, and compressed assets.

The generated site has one route:

```text
/
```

Opening `/` starts the packaged klooie app.

## Windows EXE Packaging

```powershell
kpack SomeCSProj.csproj -type EXE
```

This publishes a Windows x64 self-contained single-file executable to:

```text
SomeProject\bin\klooie.win\SomeCSProj.exe
```

## Web Entrypoint

For web packaging, mark the method that should start the app:

```csharp
using klooie;

public static class DemoAppProgram
{
    [KlooieWebTarget(
        DisplayName = "DemoApp",
        Description = "Animated labels rendered through the browser console host.")]
    public static async Task MainAsync()
    {
        await new DemoApp().RunAsync();
    }
}
```

The method must be static, take no parameters, and return `Task`, `ValueTask`, or `void`.

If no `[KlooieWebTarget]` marker is present, `kpack` tries to find a no-argument static `MainAsync` or `Main` method. The marker is preferred because real apps may contain multiple possible entrypoints.

## Sample

Package the included sample app:

```powershell
dotnet run --project .\src\klooie.Packager\klooie.Packager.csproj -- .\src\klooie.blazorSampleApp\klooie.blazorSampleApp.csproj -type Web
```

Serve the output locally:

```powershell
python -m http.server 5187 --bind 127.0.0.1 --directory .\src\klooie.blazorSampleApp\bin\klooie.web
```

Then open:

```text
http://127.0.0.1:5187/
```

You should see the sample klooie app running in the browser.

The sample project also packages itself when it is built:

```powershell
dotnet build .\src\klooie.blazorSampleApp\klooie.blazorSampleApp.csproj
```

In Visual Studio, building `klooie.blazorSampleApp` runs `kpack` and refreshes `bin\klooie.web`.

The sample includes a launch profile named `klooie.blazorSampleApp Web`. Visual Studio launches executable profiles from the project's build output folder, so the profile serves `..\..\klooie.web`, which resolves to the packaged `bin\klooie.web` folder. It starts a local Python static file server at:

```text
http://127.0.0.1:5187/
```

Use Ctrl+F5 with that profile after building the sample. If Python is not on `PATH`, serve `bin\klooie.web` with any static web server.

## Output and Temporary Files

For `-type Web`, `kpack` uses these folders relative to the input project:

```text
obj\klooie.packager\web
obj\klooie.packager\publish
bin\klooie.web
```

For `-type EXE`, it uses:

```text
obj\klooie.packager\exe
bin\klooie.win
```

The final folder is recreated each time.

## Template Validation

The folded Blazor host template lives in:

```text
src\klooie.Packager\Templates\WebHost
```

Building `klooie.Packager` also builds `Templates\WebHost\klooie.Packager.WebHostTemplate.csproj`. This catches compile breaks in the template C# and Razor files even though those files are copied into generated web hosts rather than compiled into `kpack.exe`.
