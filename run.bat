@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

cd /d "%~dp0"

set "APP=KokonoeAssistant"
set "CFG=Debug"
set "TFM=net8.0-windows"
set "EXE=%~dp0bin\%CFG%\%TFM%\%APP%.exe"
set "OUT_LOG=%~dp0run_stdout.log"
set "ERR_LOG=%~dp0run_stderr.log"
set "STARTUP_LOG=%~dp0bin\%CFG%\%TFM%\startup.log"
set "CRASH_LOG=%~dp0bin\%CFG%\%TFM%\crash.log"

echo [KILL] Stopping old Kokonoe process...
taskkill /IM "%APP%.exe" /F >nul 2>nul
timeout /t 1 /nobreak >nul

echo [BUILD] Cleaning project through MSBuild...
dotnet clean "%~dp0KokonoeAssistant.csproj" --configuration %CFG% --nologo
if errorlevel 1 (
    echo [ERROR] dotnet clean failed.
    pause
    exit /b 1
)

echo [BUILD] Building project...
dotnet build "%~dp0KokonoeAssistant.csproj" --configuration %CFG% --nologo
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo [ERROR] EXE not found: "%EXE%"
    pause
    exit /b 1
)

echo [RUN] Starting Kokonoe Assistant...
del "%OUT_LOG%" >nul 2>nul
del "%ERR_LOG%" >nul 2>nul

"%EXE%" 1>>"%OUT_LOG%" 2>>"%ERR_LOG%"
set "APP_EXIT=%ERRORLEVEL%"

if not "%APP_EXIT%"=="0" (
    echo [ERROR] App exited with code %APP_EXIT%.
    echo [ERROR] stdout: "%OUT_LOG%"
    echo [ERROR] stderr: "%ERR_LOG%"
    echo [ERROR] startup: "%STARTUP_LOG%"
    echo [ERROR] crash: "%CRASH_LOG%"
    if exist "%ERR_LOG%" (
        echo.
        echo [STDERR]
        type "%ERR_LOG%"
    )
    if exist "%CRASH_LOG%" (
        echo.
        echo [CRASH]
        type "%CRASH_LOG%"
    )
    if exist "%STARTUP_LOG%" (
        echo.
        echo [STARTUP]
        type "%STARTUP_LOG%"
    )
    pause
    exit /b %APP_EXIT%
)

endlocal
