@echo off
setlocal
cd /d "%~dp0..\src\klooie.Web.PlaywrightTests"
if not exist node_modules call npm install
set "KLOOIE_WEB_MODE=Fast"
set "KLOOIE_WEB_PORT=5187"
set "KLOOIE_WEB_ASSUME_BUILT=false"
call npx playwright test
exit /b %errorlevel%
