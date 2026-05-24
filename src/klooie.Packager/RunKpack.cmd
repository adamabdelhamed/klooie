@echo off
setlocal

set "KPACK_CONFIGURATION=Debug"
if not "%KLOOIE_PACKAGER_CONFIGURATION%"=="" set "KPACK_CONFIGURATION=%KLOOIE_PACKAGER_CONFIGURATION%"
echo %CD% | findstr /I "\\Release\\" >nul
if not errorlevel 1 set "KPACK_CONFIGURATION=Release"
echo %CD% | findstr /I "\\Debug\\" >nul
if not errorlevel 1 set "KPACK_CONFIGURATION=Debug"

set "SOURCE_DIR=%~dp0bin\%KPACK_CONFIGURATION%\net10.0"
set "RUN_ROOT=%TEMP%\klooie-packager-run"
set "RUN_DIR=%RUN_ROOT%\%RANDOM%%RANDOM%"

dotnet build "%~dp0klooie.Packager.csproj" -c "%KPACK_CONFIGURATION%" --nologo
if errorlevel 1 exit /b %errorlevel%

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
