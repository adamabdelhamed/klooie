param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectPath,

    [Parameter(Mandatory = $true)]
    [int] $Port
)

$ErrorActionPreference = "Stop"
$resolvedProject = Resolve-Path $ProjectPath
$projectDir = Split-Path $resolvedProject -Parent
$webRoot = Join-Path $projectDir "bin\klooie.web"

if (-not (Test-Path (Join-Path $webRoot "index.html") -PathType Leaf)) {
    throw "Expected existing web output at '$webRoot', but index.html was not found. Run a build-and-test script first."
}

& (Join-Path $PSScriptRoot "serve-static-web.ps1") -WebRoot $webRoot -Port $Port
exit $LASTEXITCODE
