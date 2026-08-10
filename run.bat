@echo off
REM Runs the minutesheet .NET Blazor app (HTTPS profile) and opens it in the browser.

set "ENV_FILE=%~dp0.env"

REM Load environment variables from .env if present in root or app directory
if exist "%ENV_FILE%" (
    for /f "usebackq eol=# tokens=1* delims==" %%A in ("%ENV_FILE%") do (
        if not "%%A"=="" set "%%A=%%B"
    )
)
if exist "%~dp0minutesheet\.env" (
    for /f "usebackq eol=# tokens=1* delims==" %%A in ("%~dp0minutesheet\.env") do (
        if not "%%A"=="" set "%%A=%%B"
    )
)

REM Prompt for credentials if not configured in .env or environment
if "%GOOGLE_APP_USER%"=="" if "%EMAIL_USER%"=="" if "%EmailSettings__User%"=="" (
    echo ======================================================
    echo Email credentials not found in environment or .env
    echo ======================================================
    set /p "INPUT_USER=Enter Gmail address (e.g. user@gmail.com): "
    if not "%INPUT_USER%"=="" (
        set "GOOGLE_APP_USER=%INPUT_USER%"
        echo GOOGLE_APP_USER=%INPUT_USER%>> "%ENV_FILE%"
    )
)

if "%GOOGLE_APP_PASSWORD%"=="" if "%EMAIL_PASSWORD%"=="" if "%EmailSettings__Password%"=="" (
    set /p "INPUT_PASS=Enter 16-character Google App Password: "
    if not "%INPUT_PASS%"=="" (
        set "GOOGLE_APP_PASSWORD=%INPUT_PASS%"
        echo GOOGLE_APP_PASSWORD=%INPUT_PASS%>> "%ENV_FILE%"
        echo Saved credentials to %ENV_FILE% for future runs.
    )
)

echo.
echo Starting Minute Sheet...
cd /d "%~dp0minutesheet"
dotnet run --launch-profile https
pause
