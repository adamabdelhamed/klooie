param(
    [Parameter(Mandatory = $true)]
    [string] $WebRoot,

    [Parameter(Mandatory = $true)]
    [int] $Port
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path $WebRoot
$prefix = "http://127.0.0.1:$Port/"
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($prefix)
$listener.Start()
Write-Host "Serving existing web output from $root at $prefix"

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        try {
            $path = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
            if ([string]::IsNullOrWhiteSpace($path)) { $path = "index.html" }
            $candidate = Join-Path $root $path
            if ((Test-Path $candidate -PathType Container)) { $candidate = Join-Path $candidate "index.html" }
            if (-not (Test-Path $candidate -PathType Leaf)) { $candidate = Join-Path $root "index.html" }

            $resolved = Resolve-Path $candidate
            if ($resolved.Path.StartsWith($root.Path, [StringComparison]::OrdinalIgnoreCase) -eq $false) {
                $context.Response.StatusCode = 403
                $context.Response.Close()
                continue
            }

            $extension = [IO.Path]::GetExtension($resolved.Path).ToLowerInvariant()
            $contentType = switch ($extension) {
                ".html" { "text/html; charset=utf-8" }
                ".js" { "text/javascript; charset=utf-8" }
                ".css" { "text/css; charset=utf-8" }
                ".json" { "application/json; charset=utf-8" }
                ".wasm" { "application/wasm" }
                ".ico" { "image/x-icon" }
                ".svg" { "image/svg+xml" }
                ".png" { "image/png" }
                ".br" { "application/octet-stream" }
                ".gz" { "application/gzip" }
                default { "application/octet-stream" }
            }

            $bytes = [IO.File]::ReadAllBytes($resolved.Path)
            $context.Response.ContentType = $contentType
            $context.Response.Headers["Cache-Control"] = "no-store"
            $context.Response.ContentLength64 = $bytes.Length
            $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $context.Response.Close()
        } catch {
            try {
                $context.Response.StatusCode = 500
                $context.Response.Close()
            } catch {
            }
        }
    }
} finally {
    $listener.Stop()
    $listener.Close()
}
