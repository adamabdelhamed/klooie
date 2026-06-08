param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectPath,

    [Parameter(Mandatory = $true)]
    [int] $Port,

    [string] $WebMode = "Fast"
)

$ErrorActionPreference = "Stop"
$repoSrc = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$packager = Join-Path $repoSrc "klooie.Packager\klooie.Packager.csproj"
$resolvedProject = Resolve-Path $ProjectPath
$projectDir = Split-Path $resolvedProject -Parent
$webRoot = Join-Path $projectDir "bin\klooie.web"

dotnet build $packager --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $packager -- $resolvedProject -type Web -webMode $WebMode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path (Join-Path $webRoot "index.html") -PathType Leaf)) {
    throw "Expected packaged web output at '$webRoot', but index.html was not found."
}

& (Join-Path $PSScriptRoot "serve-static-web.ps1") -WebRoot $webRoot -Port $Port
exit $LASTEXITCODE
