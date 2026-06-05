using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

            var webMode = options.WebMode ?? project.WebMode;

            if (options.Type == PackageType.Web)
            {
                await PackageWebWithLockAsync(project, webMode);
            }
            else if (options.Type == PackageType.Serve)
            {
                await ServeWebAsync(project, options.Port, webMode);
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

    private static async Task<string> PackageWebAsync(ProjectInfo project, KlooieWebMode webMode)
    {
        var target = WebEntryPointDiscoverer.Discover(project);
        var templateDirectory = LocateWebHostTemplateDirectory();
        var intermediateRoot = CreatePackageIntermediateDirectory(project);
        var tempDirectory = Path.Combine(intermediateRoot, "web");
        var publishDirectory = Path.Combine(intermediateRoot, "publish");
        var outputDirectory = GetWebOutputDirectory(project);
        var fingerprint = ComputeWebPackageFingerprint(project, templateDirectory, target, webMode);

        if (IsWebPackageCurrent(outputDirectory, fingerprint))
        {
            Console.WriteLine($"Klooie web package is current ({webMode} mode): {outputDirectory}");
            return outputDirectory;
        }

        Console.WriteLine($"Packaging klooie web output ({webMode} mode) to {outputDirectory}");

        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(publishDirectory);
        CopyWebHostTemplate(templateDirectory, tempDirectory);
        WriteGeneratedWebProject(project, tempDirectory, target, ShouldOptimizeWebAssembly(webMode));

        var generatedProjectPath = Path.Combine(tempDirectory, $"{GeneratedProjectName}.csproj");
        await RunDotNetAsync(
            new[]
            {
                "publish",
                generatedProjectPath,
                "-c",
                GetPublishConfiguration(webMode),
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

        ResetDirectory(outputDirectory, project.Directory);
        CopyDirectory(staticOutput, outputDirectory);
        CopyProjectAssets(project, outputDirectory);
        WriteWebPackageStamp(outputDirectory, fingerprint, webMode);
        Console.WriteLine($"Web package written to {outputDirectory}");
        return outputDirectory;
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

    private static async Task ServeWebAsync(ProjectInfo project, int port, KlooieWebMode webMode)
    {
        var requestedPort = port;
        KillProcessesListeningOnPort(requestedPort);
        WaitForPortToBeAvailable(requestedPort);

        var outputDirectory = GetWebOutputDirectory(project);
        using var listener = StartListener(requestedPort);

        Console.WriteLine($"Packaging and serving {outputDirectory} at http://127.0.0.1:{requestedPort}/ ({webMode} mode)");
        Console.WriteLine("Press Ctrl+C or stop debugging to end the server.");

        var packageTask = Task.Run(() => PackageWebWithLockAsync(project, webMode));
        WriteServerPidFile(requestedPort);
        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => ServeRequestAsync(context, outputDirectory, packageTask));
            }
        }
        finally
        {
            DeleteServerPidFile(requestedPort);
        }
    }

    private static async Task<string> PackageWebWithLockAsync(ProjectInfo project, KlooieWebMode webMode)
    {
        var lockPath = GetPackageLockPath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        await using var lockStream = await AcquirePackageLockAsync(lockPath);
        return await PackageWebAsync(project, webMode);
    }

    private static async Task<FileStream> AcquirePackageLockAsync(string lockPath)
    {
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(1000);
            }
        }
    }

    private static HttpListener StartListener(int requestedPort)
    {
        var listener = new HttpListener();
        listener.Prefixes.Clear();
        listener.Prefixes.Add($"http://127.0.0.1:{requestedPort}/");

        try
        {
            listener.Start();
            return listener;
        }
        catch
        {
            listener.Close();
            throw;
        }
    }

    private static async Task ServeRequestAsync(HttpListenerContext context, string outputDirectory, Task<string> packageTask)
    {
        try
        {
            if (!packageTask.IsCompleted)
            {
                await ServePackagingPageAsync(context);
                return;
            }

            if (packageTask.IsFaulted)
            {
                await ServePackagingErrorAsync(context, packageTask.Exception?.GetBaseException().Message ?? "Unknown packaging error.");
                return;
            }

            var requestPath = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty);
            var relativePath = string.IsNullOrWhiteSpace(requestPath) ? "index.html" : requestPath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));
            var fullRoot = Path.GetFullPath(outputDirectory);

            if (!fullPath.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(fullPath, Path.Combine(fullRoot, "index.html"), StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine(fullRoot, "index.html");
            }

            context.Response.ContentType = GetContentType(fullPath);
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            await using var stream = File.OpenRead(fullPath);
            context.Response.ContentLength64 = stream.Length;
            await stream.CopyToAsync(context.Response.OutputStream);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"kpack serve request failed: {ex.Message}");
            if (context.Response.OutputStream.CanWrite)
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }
    }

    private static async Task ServePackagingPageAsync(HttpListenerContext context)
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta http-equiv="refresh" content="1">
                <title>Packaging klooie app</title>
                <style>
                    html, body { margin: 0; height: 100%; background: #050505; color: #e6e6e6; font: 16px Consolas, 'Cascadia Mono', monospace; }
                    body { display: grid; place-items: center; }
                </style>
            </head>
            <body>Packaging fresh web output...</body>
            </html>
            """;
        await ServeTextAsync(context, html, "text/html; charset=utf-8", 200);
    }

    private static async Task ServePackagingErrorAsync(HttpListenerContext context, string message)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>klooie package failed</title>
                <style>
                    html, body { margin: 0; min-height: 100%; background: #050505; color: #f5d0d0; font: 16px Consolas, 'Cascadia Mono', monospace; }
                    body { padding: 32px; }
                    pre { white-space: pre-wrap; }
                </style>
            </head>
            <body><h1>Packaging failed</h1><pre>{{SecurityElement.Escape(message)}}</pre></body>
            </html>
            """;
        await ServeTextAsync(context, html, "text/html; charset=utf-8", 500);
    }

    private static async Task ServeTextAsync(HttpListenerContext context, string text, string contentType, int statusCode)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = contentType;
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".html") return "text/html; charset=utf-8";
        if (extension == ".js") return "text/javascript; charset=utf-8";
        if (extension == ".css") return "text/css; charset=utf-8";
        if (extension == ".json") return "application/json; charset=utf-8";
        if (extension == ".webmanifest") return "application/manifest+json; charset=utf-8";
        if (extension == ".wasm") return "application/wasm";
        if (extension == ".mp3") return "audio/mpeg";
        if (extension == ".map") return "application/json; charset=utf-8";
        if (extension == ".txt") return "text/plain; charset=utf-8";
        if (extension == ".png") return "image/png";
        if (extension == ".ico") return "image/x-icon";
        if (extension == ".svg") return "image/svg+xml";
        return "application/octet-stream";
    }

    private static void KillProcessesListeningOnPort(int port)
    {
        if (!OperatingSystem.IsWindows()) return;

        StopKnownKpackServer(port);

        var currentProcessId = Environment.ProcessId;
        var listenerProcessIds = FindWindowsTcpListenerProcessIds(port)
            .Concat(FindWindowsTcpListenerProcessIdsWithPowerShell(port))
            .Concat(FindWindowsHttpSysListenerProcessIds(port))
            .Where(pid => pid != currentProcessId && pid != 4)
            .Distinct()
            .ToArray();

        foreach (var processId in listenerProcessIds)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                Console.WriteLine($"Stopping existing process {processId} using port {port}.");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not stop process {processId} on port {port}: {ex.Message}");
            }
        }
    }

    private static void StopKnownKpackServer(int port)
    {
        var pidPath = GetServerPidPath(port);
        if (!File.Exists(pidPath)) return;

        var currentProcessId = Environment.ProcessId;
        var text = File.ReadAllText(pidPath).Trim();
        if (int.TryParse(text, out var processId) == false || processId == currentProcessId)
        {
            TryDeleteFile(pidPath);
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            Console.WriteLine($"Stopping existing kpack server process {processId} using port {port}.");
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not stop recorded kpack server process {processId} on port {port}: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(pidPath);
        }
    }

    private static void WriteServerPidFile(int port)
    {
        Directory.CreateDirectory(GetServerPidDirectory());
        File.WriteAllText(GetServerPidPath(port), Environment.ProcessId.ToString());
    }

    private static void DeleteServerPidFile(int port)
    {
        var pidPath = GetServerPidPath(port);
        if (File.Exists(pidPath) && string.Equals(File.ReadAllText(pidPath).Trim(), Environment.ProcessId.ToString(), StringComparison.Ordinal))
        {
            TryDeleteFile(pidPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static string GetServerPidPath(int port)
    {
        return Path.Combine(GetServerPidDirectory(), $"{port}.pid");
    }

    private static string GetServerPidDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "klooie-packager-servers");
    }

    private static void WaitForPortToBeAvailable(int port)
    {
        if (!OperatingSystem.IsWindows()) return;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var listenerProcessIds = FindWindowsTcpListenerProcessIds(port)
                .Concat(FindWindowsTcpListenerProcessIdsWithPowerShell(port))
                .Concat(FindWindowsHttpSysListenerProcessIds(port))
                .Where(pid => pid != Environment.ProcessId && pid != 4);

            if (listenerProcessIds.Any() == false) return;
            Thread.Sleep(100);
        }
    }

    private static IEnumerable<int> FindWindowsTcpListenerProcessIds(int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-ano");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("TCP");

        using var process = Process.Start(startInfo);
        if (process is null) yield break;

        while (process.StandardOutput.ReadLine() is { } line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || !string.Equals(parts[0], "TCP", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(parts[3], "LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
            if (!EndpointMatchesPort(parts[1], port)) continue;
            if (int.TryParse(parts[^1], out var processId)) yield return processId;
        }

        process.WaitForExit();
    }

    private static IEnumerable<int> FindWindowsTcpListenerProcessIdsWithPowerShell(int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add($"Get-NetTCPConnection -LocalPort {port} -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess");

        using var process = Process.Start(startInfo);
        if (process is null) yield break;

        while (process.StandardOutput.ReadLine() is { } line)
        {
            if (int.TryParse(line.Trim(), out var processId)) yield return processId;
        }

        process.WaitForExit();
    }

    private static IEnumerable<int> FindWindowsHttpSysListenerProcessIds(int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("http");
        startInfo.ArgumentList.Add("show");
        startInfo.ArgumentList.Add("servicestate");
        startInfo.ArgumentList.Add("view=requestq");

        using var process = Process.Start(startInfo);
        if (process is null) yield break;

        int? currentProcessId = null;
        while (process.StandardOutput.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
            {
                var idText = trimmed["ID:".Length..].Split(',', 2)[0].Trim();
                currentProcessId = int.TryParse(idText, out var id) ? id : null;
                continue;
            }

            if (currentProcessId.HasValue && IsHttpSysUrlForPort(trimmed, port))
            {
                yield return currentProcessId.Value;
            }
        }

        process.WaitForExit();
    }

    private static bool IsHttpSysUrlForPort(string line, int port)
    {
        if (!line.StartsWith("HTTP://", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("HTTPS://", StringComparison.OrdinalIgnoreCase)) return false;
        return line.Contains($":{port}/", StringComparison.OrdinalIgnoreCase) ||
               line.Contains($":{port}:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EndpointMatchesPort(string endpoint, int port)
    {
        var suffix = ":" + port.ToString();
        return endpoint.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || endpoint.EndsWith($".{port}", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyWebHostTemplate(string templateDirectory, string tempDirectory)
    {
        CopyDirectory(templateDirectory, tempDirectory, IsTemplateRuntimeFile);
    }

    private static string GetWebOutputDirectory(ProjectInfo project)
    {
        return Path.Combine(project.Directory, "bin", "klooie.web");
    }

    private static string CreatePackageIntermediateDirectory(ProjectInfo project)
    {
        var runsRoot = Path.Combine(project.Directory, "obj", "kp");
        Directory.CreateDirectory(runsRoot);
        return Directory.CreateDirectory(Path.Combine(runsRoot, $"{Environment.ProcessId:x}-{Guid.NewGuid():N}"[..20])).FullName;
    }

    private static string GetPackageLockPath(ProjectInfo project)
    {
        var projectHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(project.Path).ToUpperInvariant())))[..16];
        return Path.Combine(Path.GetTempPath(), "klooie-packager-locks", $"{projectHash}.lock");
    }

    private static bool ShouldOptimizeWebAssembly(KlooieWebMode webMode)
    {
        return webMode == KlooieWebMode.Aot;
    }

    private static string GetPublishConfiguration(KlooieWebMode webMode)
    {
        return webMode == KlooieWebMode.Fast ? "Debug" : "Release";
    }

    private static bool IsWebPackageCurrent(string outputDirectory, string fingerprint)
    {
        var stampPath = GetWebPackageStampPath(outputDirectory);
        return File.Exists(Path.Combine(outputDirectory, "index.html")) &&
               Directory.Exists(Path.Combine(outputDirectory, "_framework")) &&
               File.Exists(stampPath) &&
               string.Equals(File.ReadAllText(stampPath).Trim(), fingerprint, StringComparison.Ordinal);
    }

    private static void WriteWebPackageStamp(string outputDirectory, string fingerprint, KlooieWebMode webMode)
    {
        File.WriteAllText(GetWebPackageStampPath(outputDirectory), fingerprint);
        File.WriteAllText(Path.Combine(outputDirectory, "klooie.package.mode.txt"), webMode.ToString());
    }

    private static string GetWebPackageStampPath(string outputDirectory)
    {
        return Path.Combine(outputDirectory, "klooie.package.stamp");
    }

    private static string ComputeWebPackageFingerprint(ProjectInfo project, string templateDirectory, WebEntryPoint target, KlooieWebMode webMode)
    {
        var fingerprint = new StringBuilder();
        AddFingerprintText(fingerprint, $"mode:{webMode}");
        AddFingerprintText(fingerprint, $"target:{project.TargetFramework}");
        AddFingerprintText(fingerprint, $"title:{target.BrowserTitle}");
        AddFingerprintText(fingerprint, $"pwa:{target.PwaName}:{target.PwaShortName}");

        foreach (var file in EnumerateWebPackageInputs(project, templateDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            AddFingerprintText(fingerprint, Path.GetFullPath(file).ToUpperInvariant());
            AddFingerprintText(fingerprint, info.Length.ToString());
            AddFingerprintText(fingerprint, info.LastWriteTimeUtc.Ticks.ToString());
        }

        var iconPath = ResolveWebIconPath(project, target);
        if (iconPath is not null)
        {
            var info = new FileInfo(iconPath);
            AddFingerprintText(fingerprint, Path.GetFullPath(iconPath).ToUpperInvariant());
            AddFingerprintText(fingerprint, info.Length.ToString());
            AddFingerprintText(fingerprint, info.LastWriteTimeUtc.Ticks.ToString());
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString())));
    }

    private static IEnumerable<string> EnumerateWebPackageInputs(ProjectInfo project, string templateDirectory)
    {
        var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in EnumerateProjectGraph(project.Path, visitedProjects))
        {
            yield return projectPath;
            var projectDirectory = Path.GetDirectoryName(projectPath)!;
            foreach (var file in Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories).Where(IsRelevantProjectInput))
            {
                yield return file;
            }
        }

        foreach (var file in Directory.EnumerateFiles(templateDirectory, "*", SearchOption.AllDirectories).Where(IsRelevantProjectInput))
        {
            yield return file;
        }

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories).Where(IsPackagerRuntimeInput))
        {
            yield return file;
        }
    }

    private static IEnumerable<string> EnumerateProjectGraph(string projectPath, HashSet<string> visitedProjects)
    {
        projectPath = Path.GetFullPath(projectPath);
        if (!visitedProjects.Add(projectPath)) yield break;

        yield return projectPath;

        XDocument document;
        try
        {
            document = XDocument.Load(projectPath);
        }
        catch
        {
            yield break;
        }

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var references = document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => string.IsNullOrWhiteSpace(include) == false)
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .Where(File.Exists);

        foreach (var reference in references)
        {
            foreach (var transitiveReference in EnumerateProjectGraph(reference, visitedProjects))
            {
                yield return transitiveReference;
            }
        }
    }

    private static bool IsRelevantProjectInput(string path)
    {
        if (IsBuildArtifactPath(path)) return false;

        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".cs" ||
               extension == ".csproj" ||
               extension == ".props" ||
               extension == ".targets" ||
               extension == ".json" ||
               extension == ".razor" ||
               extension == ".html" ||
               extension == ".css" ||
               extension == ".js" ||
               path.Contains($"{Path.DirectorySeparatorChar}Assets{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackagerRuntimeInput(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension == ".dll" ||
               extension == ".deps.json" ||
               extension == ".runtimeconfig.json";
    }

    private static bool IsBuildArtifactPath(string path)
    {
        var parts = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(part, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddFingerprintText(StringBuilder fingerprint, string text)
    {
        fingerprint.Append(text);
        fingerprint.Append('\0');
    }

    private static void WriteGeneratedWebProject(ProjectInfo project, string tempDirectory, WebEntryPoint target, bool optimizeWebAssembly)
    {
        var projectReference = SecurityElement.Escape(project.Path);
        var targetFramework = SecurityElement.Escape(project.TargetFramework);
        var optimizeWebAssemblyText = optimizeWebAssembly.ToString().ToLowerInvariant();
        var branding = CopyWebBrandingAssets(project, tempDirectory, target);
        var loadingHtmlPath = ToWebAssetUrl(target.LoadingHtmlAssetPath);
        var stoppedHtmlPath = ToWebAssetUrl(target.StoppedHtmlAssetPath);

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
                <PublishTrimmed>{optimizeWebAssemblyText}</PublishTrimmed>
                <RunAOTCompilation>{optimizeWebAssemblyText}</RunAOTCompilation>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.5" />
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.5" PrivateAssets="all" />
                <PackageReference Include="System.Formats.Nrbf" Version="10.0.8" />
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
                    runAsync: {{target.ToFuncTaskExpression()}},
                    mobileOptions: new KlooieBlazorMobileOptions(
                        RequireHorizontal: {{target.RequireHorizontal.ToString().ToLowerInvariant()}},
                        TouchTriggerToggle: {{target.TouchTriggerToggle.ToString().ToLowerInvariant()}},
                        ZoomMin: {{target.MobileZoomMin.ToString(CultureInfo.InvariantCulture)}},
                        ZoomDefault: {{target.MobileZoomDefault.ToString(CultureInfo.InvariantCulture)}},
                        ZoomMax: {{target.MobileZoomMax.ToString(CultureInfo.InvariantCulture)}}),
                    browserMetadata: new KlooieBlazorBrowserMetadata(
                        BrowserTitle: "{{EscapeCSharpString(target.BrowserTitle)}}",
                        PwaName: "{{EscapeCSharpString(target.PwaName)}}",
                        PwaShortName: "{{EscapeCSharpString(target.PwaShortName)}}",
                        Description: "{{EscapeCSharpString(target.Description)}}",
                        ThemeColor: "{{EscapeCSharpString(target.ThemeColor)}}",
                        BackgroundColor: "{{EscapeCSharpString(target.BackgroundColor)}}",
                        FaviconPath: "{{EscapeCSharpString(branding.FaviconPath)}}",
                        AppIconPath: "{{EscapeCSharpString(branding.AppIconPath)}}"),
                    lifecycleOptions: new KlooieBlazorLifecycleOptions(
                        LoadingHtmlPath: {{ToNullableCSharpString(loadingHtmlPath)}},
                        StoppedHtmlPath: {{ToNullableCSharpString(stoppedHtmlPath)}}));
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
            $$"""
            <!DOCTYPE html>
            <html lang="en">

            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
                <title>{{WebUtility.HtmlEncode(target.BrowserTitle)}}</title>
                <base href="/" />
                <link rel="manifest" href="manifest.webmanifest" />
                <link rel="icon" href="{{WebUtility.HtmlEncode(branding.FaviconPath)}}" />
                <link rel="shortcut icon" href="{{WebUtility.HtmlEncode(branding.FaviconPath)}}" />
                <link rel="apple-touch-icon" href="{{WebUtility.HtmlEncode(branding.AppIconPath)}}" />
                <meta name="application-name" content="{{WebUtility.HtmlEncode(target.PwaName)}}" />
                <meta name="apple-mobile-web-app-title" content="{{WebUtility.HtmlEncode(target.PwaShortName)}}" />
                <meta name="description" content="{{WebUtility.HtmlEncode(target.Description)}}" />
                <meta name="theme-color" content="{{WebUtility.HtmlEncode(target.ThemeColor)}}" />
                <link rel="preload" id="webassembly" />
                <link rel="stylesheet" href="css/app.css" />
                <script type="importmap"></script>
                <script>
                    window.klooieLifecycleOptions = {
                        loadingHtmlPath: {{ToJsonStringLiteral(loadingHtmlPath)}},
                        stoppedHtmlPath: {{ToJsonStringLiteral(stoppedHtmlPath)}}
                    };
                </script>
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
                <script>
                    if ("serviceWorker" in navigator && (location.protocol === "https:" || location.hostname === "localhost" || location.hostname === "127.0.0.1")) {
                        navigator.serviceWorker.register("service-worker.js").catch(() => {});
                    }
                </script>
                <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
            </body>

            </html>
            """);

        File.WriteAllText(
            Path.Combine(tempDirectory, "wwwroot", "manifest.webmanifest"),
            JsonSerializer.Serialize(new
            {
                name = target.PwaName,
                short_name = target.PwaShortName,
                description = target.Description,
                start_url = ".",
                scope = ".",
                display = "fullscreen",
                display_override = new[] { "fullscreen", "standalone", "minimal-ui" },
                background_color = target.BackgroundColor,
                theme_color = target.ThemeColor,
                orientation = target.RequireHorizontal ? "landscape" : "any",
                icons = branding.ManifestIcons
            }));
    }

    private static WebBrandingAssets CopyWebBrandingAssets(ProjectInfo project, string tempDirectory, WebEntryPoint target)
    {
        var defaultIcon = "icon.svg";
        var iconPath = ResolveWebIconPath(project, target);
        if (iconPath is null)
        {
            return new WebBrandingAssets(
                defaultIcon,
                defaultIcon,
                new[]
                {
                    new WebManifestIcon(defaultIcon, "any", "image/svg+xml", "any maskable")
                });
        }

        var wwwroot = Path.Combine(tempDirectory, "wwwroot");
        var extension = Path.GetExtension(iconPath).ToLowerInvariant();
        var faviconPath = extension == ".ico" ? "app-icon.ico" : "app-icon" + extension;
        var faviconDestination = Path.Combine(wwwroot, faviconPath);
        CopyFile(iconPath, faviconDestination);

        var manifestIcons = new List<WebManifestIcon>();
        var appIconPath = faviconPath;
        if (extension == ".ico" && TryExtractLargestPngFromIco(iconPath, out var pngBytes, out var pngSize))
        {
            appIconPath = "app-icon.png";
            File.WriteAllBytes(Path.Combine(wwwroot, appIconPath), pngBytes);
            manifestIcons.Add(new WebManifestIcon(appIconPath, $"{pngSize}x{pngSize}", "image/png", "any maskable"));
        }

        var type = extension == ".ico" ? "image/x-icon" : GetIconContentType(extension);
        manifestIcons.Add(new WebManifestIcon(faviconPath, "any", type, "any"));
        return new WebBrandingAssets(faviconPath, appIconPath, manifestIcons.ToArray());
    }

    private static string? ResolveWebIconPath(ProjectInfo project, WebEntryPoint target)
    {
        if (string.IsNullOrWhiteSpace(target.IconPath)) return null;

        var path = target.IconPath!;
        if (Path.IsPathRooted(path) == false)
        {
            path = Path.Combine(project.Directory, path);
        }

        path = Path.GetFullPath(path);
        if (File.Exists(path) == false)
        {
            throw new FileNotFoundException($"Web icon file '{target.IconPath}' was not found.", path);
        }

        return path;
    }

    private static string GetIconContentType(string extension)
    {
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase)) return "image/svg+xml";
        if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        return "application/octet-stream";
    }

    private static bool TryExtractLargestPngFromIco(string iconPath, out byte[] pngBytes, out int size)
    {
        pngBytes = [];
        size = 0;

        var bytes = File.ReadAllBytes(iconPath);
        if (bytes.Length < 6 || BitConverter.ToUInt16(bytes, 0) != 0 || BitConverter.ToUInt16(bytes, 2) != 1) return false;

        var count = BitConverter.ToUInt16(bytes, 4);
        for (var i = 0; i < count; i++)
        {
            var entryOffset = 6 + i * 16;
            if (entryOffset + 16 > bytes.Length) break;

            var width = bytes[entryOffset] == 0 ? 256 : bytes[entryOffset];
            var height = bytes[entryOffset + 1] == 0 ? 256 : bytes[entryOffset + 1];
            var byteCount = BitConverter.ToUInt32(bytes, entryOffset + 8);
            var imageOffset = BitConverter.ToUInt32(bytes, entryOffset + 12);
            if (imageOffset + byteCount > bytes.Length || byteCount < 8) continue;
            if (bytes[imageOffset] != 0x89 || bytes[imageOffset + 1] != 0x50 || bytes[imageOffset + 2] != 0x4E || bytes[imageOffset + 3] != 0x47) continue;
            if (width < size || height < size) continue;

            size = Math.Min(width, height);
            pngBytes = bytes.Skip((int)imageOffset).Take((int)byteCount).ToArray();
        }

        return pngBytes.Length > 0;
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

        Directory.CreateDirectory(fullPath);
        ClearDirectoryWithRetry(fullPath);
    }

    private static void ClearDirectoryWithRetry(string fullPath)
    {
        if (!Directory.Exists(fullPath)) return;

        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(fullPath))
                {
                    File.Delete(file);
                }

                foreach (var directory in Directory.EnumerateDirectories(fullPath))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == maxAttempts)
                {
                    throw;
                }

                Thread.Sleep(500);
            }
        }
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

    private static void CopyProjectAssets(ProjectInfo project, string outputDirectory)
    {
        var sourceDirectory = Path.Combine(project.Directory, "Assets");
        if (!Directory.Exists(sourceDirectory)) return;

        var destinationDirectory = Path.Combine(outputDirectory, "assets");
        CopyDirectory(sourceDirectory, destinationDirectory);

        var manifest = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !IsBuildArtifactDirectory(sourceDirectory, Path.GetDirectoryName(file)!))
            .Select(file => Path.GetRelativePath(sourceDirectory, file).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        File.WriteAllText(Path.Combine(destinationDirectory, "klooie-assets.json"), JsonSerializer.Serialize(manifest));
    }

    private static string EscapeCSharpString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ToNullableCSharpString(string? value)
    {
        return value is null ? "null" : $"\"{EscapeCSharpString(value)}\"";
    }

    private static string ToJsonStringLiteral(string? value)
    {
        return value is null ? "null" : JsonSerializer.Serialize(value);
    }

    private static string? ToWebAssetUrl(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return null;

        assetPath = assetPath.Trim().Replace('\\', '/').TrimStart('/');
        if (assetPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
        {
            return assetPath;
        }

        return $"assets/{assetPath}";
    }
}

internal sealed record PackageOptions(string ProjectPath, PackageType Type, int Port, KlooieWebMode? WebMode)
{
    public static PackageOptions Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(arg => arg is "-h" or "--help" or "/?"))
        {
            throw new InvalidOperationException("Usage: kpack <project.csproj> -type Web|EXE|Serve [-port 5188] [-webMode Fast|Aot]");
        }

        var projectPath = Path.GetFullPath(args[0]);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Project file not found.", projectPath);
        }

        var type = PackageType.Web;
        var port = 5188;
        KlooieWebMode? webMode = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-type", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length)
                {
                    throw new InvalidOperationException("-type requires Web, EXE, or Serve.");
                }

                var typeName = args[i];
                if (string.Equals(typeName, "Web", StringComparison.OrdinalIgnoreCase)) type = PackageType.Web;
                else if (string.Equals(typeName, "EXE", StringComparison.OrdinalIgnoreCase)) type = PackageType.Exe;
                else if (string.Equals(typeName, "Serve", StringComparison.OrdinalIgnoreCase)) type = PackageType.Serve;
                else throw new InvalidOperationException("-type requires Web, EXE, or Serve.");
                continue;
            }

            if (string.Equals(args[i], "-webMode", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || TryParseWebMode(args[i], out var parsedWebMode) == false)
                {
                    throw new InvalidOperationException("-webMode requires Fast or Aot.");
                }

                webMode = parsedWebMode;
                continue;
            }

            if (string.Equals(args[i], "-port", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length || int.TryParse(args[i], out port) == false || port < 1 || port > 65535)
                {
                    throw new InvalidOperationException("-port requires a valid TCP port.");
                }
                continue;
            }

            throw new InvalidOperationException($"Unknown argument '{args[i]}'.");
        }

        return new PackageOptions(projectPath, type, port, webMode);
    }

    public static bool TryParseWebMode(string value, out KlooieWebMode webMode)
    {
        if (string.Equals(value, "Fast", StringComparison.OrdinalIgnoreCase))
        {
            webMode = KlooieWebMode.Fast;
            return true;
        }

        if (string.Equals(value, "Aot", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "AOT", StringComparison.OrdinalIgnoreCase))
        {
            webMode = KlooieWebMode.Aot;
            return true;
        }

        webMode = KlooieWebMode.Fast;
        return false;
    }
}

internal enum PackageType
{
    Web,
    Exe,
    Serve
}

internal enum KlooieWebMode
{
    Fast,
    Aot
}

internal sealed record ProjectInfo(string Path, string Directory, string AssemblyName, string RootNamespace, string TargetFramework, KlooieWebMode WebMode)
{
    public static ProjectInfo Load(string path)
    {
        var document = XDocument.Load(path);
        var projectName = System.IO.Path.GetFileNameWithoutExtension(path);
        var assemblyName = ReadProperty(document, "AssemblyName") ?? projectName;
        var rootNamespace = ReadProperty(document, "RootNamespace") ?? assemblyName;
        var targetFramework = ReadProperty(document, "TargetFramework") ?? ReadProperty(document, "TargetFrameworks")?.Split(';')[0] ?? "net10.0";
        var webModeText = ReadProperty(document, "KlooieWebMode") ?? "Fast";
        if (PackageOptions.TryParseWebMode(webModeText, out var webMode) == false)
        {
            throw new InvalidOperationException($"KlooieWebMode in '{path}' must be Fast or Aot.");
        }

        return new ProjectInfo(
            System.IO.Path.GetFullPath(path),
            System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!,
            assemblyName,
            rootNamespace,
            targetFramework,
            webMode);
    }

    private static string? ReadProperty(XDocument document, string name)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == name)
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => value.Length > 0);
    }
}

internal sealed record WebBrandingAssets(string FaviconPath, string AppIconPath, WebManifestIcon[] ManifestIcons);

internal sealed record WebManifestIcon(string src, string sizes, string type, string purpose);

internal sealed record WebEntryPoint(
    string TypeName,
    string MethodName,
    string ReturnType,
    string DisplayName,
    string BrowserTitle,
    string PwaName,
    string PwaShortName,
    string Description,
    string? IconPath,
    string? LoadingHtmlAssetPath,
    string? StoppedHtmlAssetPath,
    string ThemeColor,
    string BackgroundColor,
    bool RequireHorizontal,
    bool TouchTriggerToggle,
    double MobileZoomMin,
    double MobileZoomDefault,
    double MobileZoomMax)
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
            ReadAttributeString(attributeSource, "BrowserTitle") ?? ReadAttributeString(attributeSource, "DisplayName") ?? project.AssemblyName,
            ReadAttributeString(attributeSource, "PwaName") ?? ReadAttributeString(attributeSource, "DisplayName") ?? project.AssemblyName,
            ReadAttributeString(attributeSource, "PwaShortName") ?? ReadAttributeString(attributeSource, "PwaName") ?? ReadAttributeString(attributeSource, "DisplayName") ?? project.AssemblyName,
            ReadAttributeString(attributeSource, "Description") ?? "Packaged klooie app.",
            ReadAttributeString(attributeSource, "IconPath"),
            ReadAttributeString(attributeSource, "LoadingHtmlAssetPath"),
            ReadAttributeString(attributeSource, "StoppedHtmlAssetPath"),
            ReadAttributeString(attributeSource, "ThemeColor") ?? "#000000",
            ReadAttributeString(attributeSource, "BackgroundColor") ?? "#000000",
            ReadAttributeBool(attributeSource, "RequireHorizontal"),
            ReadAttributeBool(attributeSource, "TouchTriggerToggle"),
            ReadAttributeDouble(attributeSource, "MobileZoomMin", 0.6),
            ReadAttributeDouble(attributeSource, "MobileZoomDefault", 0.6),
            ReadAttributeDouble(attributeSource, "MobileZoomMax", 1.3));
    }

    private static string? ReadAttributeString(string? attributeSource, string propertyName)
    {
        if (attributeSource is null) return null;

        var match = Regex.Match(attributeSource, $@"\b{Regex.Escape(propertyName)}\s*=\s*""(?<value>[^""]*)""");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static bool ReadAttributeBool(string? attributeSource, string propertyName)
    {
        if (attributeSource is null) return false;

        var match = Regex.Match(attributeSource, $@"\b{Regex.Escape(propertyName)}\s*=\s*(?<value>true|false)", RegexOptions.IgnoreCase);
        return match.Success && bool.TryParse(match.Groups["value"].Value, out var value) && value;
    }

    private static double ReadAttributeDouble(string? attributeSource, string propertyName, double defaultValue)
    {
        if (attributeSource is null) return defaultValue;

        var match = Regex.Match(attributeSource, $@"\b{Regex.Escape(propertyName)}\s*=\s*(?<value>-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups["value"].Value, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
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
