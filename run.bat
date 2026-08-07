@echo off
REM Runs the minutesheet .NET Blazor app (HTTPS profile) and opens it in the browser.
cd /d "%~dp0minutesheet"
dotnet run --launch-profile https
pause
