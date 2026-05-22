using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace klooie.Packager;

internal static class Program
{
    private const string GeneratedProjectName = "klooie.web.host";
    private const string RootRoute = "__klooie";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = PackageOptions.Parse(args);
            var project = ProjectInfo.Load(options.ProjectPath);

            if (options.Type == PackageType.Web)
            {
                await PackageWebAsync(project);
            }
            else
            {
                await PackageExeAsync(project);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"kpack: {ex.Message}");
            return 1;
        }
    }

    private static async Task PackageWebAsync(ProjectInfo project)
    {
        var target = WebEntryPointDiscoverer.Discover(project);
        var templateDirectory = LocateWebHostTemplateDirectory();

        var tempDirectory = Path.Combine(project.Directory, "obj", "klooie.packager", "web");
        var publishDirectory = Path.Combine(project.Directory, "obj", "klooie.packager", "publish");
        var outputDirectory = Path.Combine(project.Directory, "bin", "klooie.web");

        ResetDirectory(tempDirectory, project.Directory);
        ResetDirectory(publishDirectory, project.Directory);
        ResetDirectory(outputDirectory, project.Directory);

        CopyWebHostTemplate(templateDirectory, tempDirectory);
        WriteGeneratedWebProject(project, tempDirectory, target);

        var generatedProjectPath = Path.Combine(tempDirectory, $"{GeneratedProjectName}.csproj");
        await RunDotNetAsync(
            new[]
            {
                "publish",
                generatedProjectPath,
                "-c",
                "Release",
                "-p:KlooiePackageOnBuild=false",
                "-o",
                publishDirectory,
                "--nologo"
            },
            project.Directory);

        var staticOutput = Path.Combine(publishDirectory, "wwwroot");
        if (!Directory.Exists(staticOutput))
        {
            throw new InvalidOperationException($"Publish did not produce static web output at '{staticOutput}'.");
        }

        CopyDirectory(staticOutput, outputDirectory);
        Console.WriteLine($"Web package written to {outputDirectory}");
    }

    private static async Task PackageExeAsync(ProjectInfo project)
    {
        var publishDirectory = Path.Combine(project.Directory, "obj", "klooie.packager", "exe");
        var outputDirectory = Path.Combine(project.Directory, "bin", "klooie.win");

        ResetDirectory(publishDirectory, project.Directory);
        ResetDirectory(outputDirectory, project.Directory);

        await RunDotNetAsync(
            new[]
            {
                "publish",
                project.Path,
                "-c",
                "Release",
                "-p:KlooiePackageOnBuild=false",
                "-r",
                "win-x64",
                "--self-contained",
                "true",
                "-p:OutputType=Exe",
                "-p:PublishSingleFile=true",
                $"-p:AssemblyName={project.AssemblyName}",
                "-o",
                publishDirectory,
                "--nologo"
            },
            project.Directory);

        var exePath = Path.Combine(publishDirectory, $"{project.AssemblyName}.exe");
        if (!File.Exists(exePath))
        {
            throw new InvalidOperationException($"Publish did not produce '{exePath}'.");
        }

        File.Copy(exePath, Path.Combine(outputDirectory, $"{project.AssemblyName}.exe"), overwrite: true);
        Console.WriteLine($"Windows executable written to {Path.Combine(outputDirectory, $"{project.AssemblyName}.exe")}");
    }

    private static void CopyWebHostTemplate(string templateDirectory, string tempDirectory)
    {
        CopyDirectory(templateDirectory, tempDirectory, IsTemplateRuntimeFile);
    }

    private static void WriteGeneratedWebProject(ProjectInfo project, string tempDirectory, WebEntryPoint target)
    {
        var projectReference = SecurityElement.Escape(project.Path);
        var targetFramework = SecurityElement.Escape(project.TargetFramework);

        File.WriteAllText(
            Path.Combine(tempDirectory, $"{GeneratedProjectName}.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <RootNamespace>klooie.blazor</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.5" />
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.5" PrivateAssets="all" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="{projectReference}" />
              </ItemGroup>

            </Project>
            """);

        File.WriteAllText(
            Path.Combine(tempDirectory, "Program.cs"),
            $$"""
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
                    route: "{{RootRoute}}",
                    displayName: "{{EscapeCSharpString(target.DisplayName)}}",
                    description: "{{EscapeCSharpString(target.Description)}}",
                    runAsync: {{target.ToFuncTaskExpression()}});
                return registry;
            });

            await builder.Build().RunAsync();
            """);

        File.WriteAllText(
            Path.Combine(tempDirectory, "Pages", "Home.razor"),
            $$"""
            @page "/"

            <KlooieAppHost AppRoute="{{RootRoute}}" />
            """);

        File.WriteAllText(
            Path.Combine(tempDirectory, "wwwroot", "index.html"),
            """
            <!DOCTYPE html>
            <html lang="en">

            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>klooie</title>
                <base href="/" />
                <link rel="preload" id="webassembly" />
                <link rel="stylesheet" href="css/app.css" />
                <script type="importmap"></script>
            </head>

            <body>
                <div id="app">
                    <svg class="loading-progress">
                        <circle r="40%" cx="50%" cy="50%" />
                        <circle r="40%" cx="50%" cy="50%" />
                    </svg>
                    <div class="loading-progress-text"></div>
                </div>

                <div id="blazor-error-ui">
                    An unhandled error has occurred.
                    <a href="." class="reload">Reload</a>
                    <span class="dismiss">X</span>
                </div>
                <script src="klooieFramePump.js"></script>
                <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
            </body>

            </html>
            """);
    }

    private static string LocateWebHostTemplateDirectory()
    {
        foreach (var candidate in CandidateTemplateDirectories())
        {
            if (IsWebHostTemplateDirectory(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not locate the packaged WebHost template files.");
    }

    private static IEnumerable<string> CandidateTemplateDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Templates", "WebHost");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Templates", "WebHost");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "src", "klooie.Packager", "Templates", "WebHost");
    }

    private static bool IsWebHostTemplateDirectory(string path)
    {
        return File.Exists(Path.Combine(path, "App.razor")) &&
               File.Exists(Path.Combine(path, "_Imports.razor")) &&
               File.Exists(Path.Combine(path, "wwwroot", "klooieFramePump.js")) &&
               Directory.Exists(Path.Combine(path, "BrowserConsole")) &&
               Directory.Exists(Path.Combine(path, "Hosting")) &&
               Directory.Exists(Path.Combine(path, "Pages"));
    }

    private static async Task RunDotNetAsync(IEnumerable<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var outputTask = RelayAsync(process.StandardOutput, Console.Out);
        var errorTask = RelayAsync(process.StandardError, Console.Error);

        await process.WaitForExitAsync();
        await Task.WhenAll(outputTask, errorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.");
        }
    }

    private static async Task RelayAsync(StreamReader reader, TextWriter writer)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            writer.WriteLine(line);
        }
    }

    private static void ResetDirectory(string path, string guardRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullGuard = Path.GetFullPath(guardRoot);
        if (!fullPath.StartsWith(fullGuard.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to clean path outside the project directory: {fullPath}");
        }

        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
        Directory.CreateDirectory(fullPath);
    }

    private static void CopyDirectory(string source, string destination, Func<string, bool>? includeFile = null)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (IsBuildArtifactDirectory(source, directory)) continue;
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (IsBuildArtifactDirectory(source, Path.GetDirectoryName(file)!)) continue;
            if (includeFile is not null && !includeFile(file)) continue;
            CopyFile(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
        }
    }

    private static bool IsTemplateRuntimeFile(string file)
    {
        return !string.Equals(Path.GetFileName(file), "klooie.Packager.WebHostTemplate.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuildArtifactDirectory(string source, string path)
    {
        var relativeParts = Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relativeParts.Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static void CopyFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static string EscapeCSharpString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

internal sealed record PackageOptions(string ProjectPath, PackageType Type)
{
    public static PackageOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(arg => arg is "-h" or "--help" or "/?"))
        {
            throw new InvalidOperationException("Usage: kpack <project.csproj> -type Web|EXE");
        }

        var projectPath = Path.GetFullPath(args[0]);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Project file not found.", projectPath);
        }

        var type = PackageType.Web;
        for (var i = 1; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "-type", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unknown argument '{args[i]}'.");
            }

            if (++i >= args.Length)
            {
                throw new InvalidOperationException("-type requires Web or EXE.");
            }

            type = args[i].ToUpperInvariant() switch
            {
                "WEB" => PackageType.Web,
                "EXE" => PackageType.Exe,
                _ => throw new InvalidOperationException("-type requires Web or EXE.")
            };
        }

        return new PackageOptions(projectPath, type);
    }
}

internal enum PackageType
{
    Web,
    Exe
}

internal sealed record ProjectInfo(string Path, string Directory, string AssemblyName, string RootNamespace, string TargetFramework)
{
    public static ProjectInfo Load(string path)
    {
        var document = XDocument.Load(path);
        var projectName = System.IO.Path.GetFileNameWithoutExtension(path);
        var assemblyName = ReadProperty(document, "AssemblyName") ?? projectName;
        var rootNamespace = ReadProperty(document, "RootNamespace") ?? assemblyName;
        var targetFramework = ReadProperty(document, "TargetFramework") ?? ReadProperty(document, "TargetFrameworks")?.Split(';')[0] ?? "net10.0";

        return new ProjectInfo(
            System.IO.Path.GetFullPath(path),
            System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!,
            assemblyName,
            rootNamespace,
            targetFramework);
    }

    private static string? ReadProperty(XDocument document, string name)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);
    }
}

internal sealed record WebEntryPoint(
    string TypeName,
    string MethodName,
    string ReturnType,
    string DisplayName,
    string Description)
{
    public string ToFuncTaskExpression()
    {
        var invocation = $"global::{TypeName}.{MethodName}()";
        return ReturnType switch
        {
            "Task" => $"global::{TypeName}.{MethodName}",
            "ValueTask" => $"async () => await {invocation}",
            "void" => $"() => {{ {invocation}; return Task.CompletedTask; }}",
            _ => $"global::{TypeName}.{MethodName}"
        };
    }
}

internal static class WebEntryPointDiscoverer
{
    private static readonly Regex NamespaceRegex = new(@"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
    private static readonly Regex TypeRegex = new(@"\b(?:class|struct|record\s+class|record)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex MarkedMethodRegex = new(@"\[[^\]]*KlooieWebTarget[^\]]*\][\s\[\]\w\(\),""=.]*?\bstatic\s+(?:async\s+)?(?<return>Task|ValueTask|void)\s+(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\)", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex CandidateMethodRegex = new(@"\bstatic\s+(?:async\s+)?(?<return>Task|ValueTask|void)\s+(?<method>MainAsync|Main)\s*\(\s*\)", RegexOptions.Compiled);
    private static readonly Regex MarkedTypeRegex = new(@"\[[^\]]*KlooieWebTarget[^\]]*\][\s\[\]\w\(\),""=.]*?\b(?:class|struct|record\s+class|record)\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.Singleline);

    public static WebEntryPoint Discover(ProjectInfo project)
    {
        var sourceFiles = Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            var markedMethod = MarkedMethodRegex.Match(text);
            if (markedMethod.Success)
            {
                return CreateEntryPoint(project, text, markedMethod.Index, markedMethod.Groups["method"].Value, markedMethod.Groups["return"].Value, markedMethod.Value);
            }

            var markedType = MarkedTypeRegex.Match(text);
            if (markedType.Success)
            {
                var typeBodyStart = text.IndexOf('{', markedType.Index);
                if (typeBodyStart >= 0)
                {
                    var typeBodyEnd = FindMatchingBrace(text, typeBodyStart);
                    var typeBody = typeBodyEnd > typeBodyStart ? text.Substring(typeBodyStart, typeBodyEnd - typeBodyStart) : text[typeBodyStart..];
                    var method = CandidateMethodRegex.Match(typeBody);
                    if (method.Success)
                    {
                        return CreateEntryPoint(project, text, markedType.Index + markedType.Length, method.Groups["method"].Value, method.Groups["return"].Value, markedType.Value);
                    }
                }
            }
        }

        var candidates = new List<WebEntryPoint>();
        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match method in CandidateMethodRegex.Matches(text))
            {
                candidates.Add(CreateEntryPoint(project, text, method.Index, method.Groups["method"].Value, method.Groups["return"].Value));
            }
        }

        var preferred = candidates
            .OrderBy(candidate => candidate.MethodName == "MainAsync" ? 0 : 1)
            .ThenBy(candidate => candidate.TypeName.EndsWith($".{project.AssemblyName}Program", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(candidate => candidate.TypeName.EndsWith(".DemoAppProgram", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(candidate => candidate.TypeName.EndsWith(".Program", StringComparison.Ordinal) ? 0 : 1)
            .FirstOrDefault();

        return preferred ?? throw new InvalidOperationException("Could not find a web entry point. Add [KlooieWebTarget] to a public static MainAsync method with no parameters.");
    }

    private static WebEntryPoint CreateEntryPoint(ProjectInfo project, string source, int index, string methodName, string returnType, string? attributeSource = null)
    {
        var namespaceName = NamespaceRegex.Matches(source[..index]).LastOrDefault()?.Groups[1].Value ?? project.RootNamespace;
        var typeName = TypeRegex.Matches(source[..index]).LastOrDefault()?.Groups[1].Value;
        if (typeName is null)
        {
            throw new InvalidOperationException($"Found {methodName}, but could not determine its declaring type.");
        }

        return new WebEntryPoint(
            $"{namespaceName}.{typeName}",
            methodName,
            returnType,
            ReadAttributeString(attributeSource, "DisplayName") ?? project.AssemblyName,
            ReadAttributeString(attributeSource, "Description") ?? "Packaged klooie app.");
    }

    private static string? ReadAttributeString(string? attributeSource, string propertyName)
    {
        if (attributeSource is null) return null;

        var match = Regex.Match(attributeSource, $@"\b{Regex.Escape(propertyName)}\s*=\s*""(?<value>[^""]*)""");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static int FindMatchingBrace(string text, int openBraceIndex)
    {
        var depth = 0;
        for (var i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            if (text[i] == '}') depth--;
            if (depth == 0) return i;
        }

        return text.Length - 1;
    }
}
