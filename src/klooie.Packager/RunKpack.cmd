@echo off
setlocal

set "SOURCE_DIR=%~dp0bin\Debug\net10.0"
set "RUN_ROOT=%TEMP%\klooie-packager-run"
set "RUN_DIR=%RUN_ROOT%\%RANDOM%%RANDOM%"

if not exist "%SOURCE_DIR%\kpack.dll" (
    echo kpack.dll was not found at "%SOURCE_DIR%\kpack.dll".
    echo Build klooie.Packager before launching.
    exit /b 1
)

mkdir "%RUN_DIR%" >nul 2>nul
robocopy "%SOURCE_DIR%" "%RUN_DIR%" /MIR /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 exit /b %errorlevel%

dotnet "%RUN_DIR%\kpack.dll" %*
exit /b %errorlevel%
