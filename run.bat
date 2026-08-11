@echo off
REM Runs the minutesheet .NET Blazor app on the HTTP development port.

set "ENV_FILE=%~dp0.env"

REM Load environment variables from root .env if present
if exist "%ENV_FILE%" (
    for /f "usebackq eol=# tokens=1* delims==" %%A in ("%ENV_FILE%") do (
        if not "%%A"=="" set "%%A=%%B"
    )
)

REM Load environment variables from minutesheet\.env if present
if exist "%~dp0minutesheet\.env" (
    for /f "usebackq eol=# tokens=1* delims==" %%A in ("%~dp0minutesheet\.env") do (
        if not "%%A"=="" set "%%A=%%B"
    )
)

echo Starting Minute Sheet...
cd /d "%~dp0minutesheet"
dotnet run --launch-profile http
pause
