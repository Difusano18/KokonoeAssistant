@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"

echo [KILL] Убиваю старий процес...
taskkill /IM KokonoeAssistant.exe /F 2>nul
timeout /t 1 /nobreak >nul

echo [BUILD] Очищення та видалення bin папки...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo [BUILD] Компіляція проекту...
dotnet build --configuration Debug --nologo

if errorlevel 1 (
    echo [ERROR] Білд не вдався!
    pause
    exit /b 1
)

echo [RUN] Запуск Kokonoe Assistant...
timeout /t 1 /nobreak >nul

REM Run in foreground so we can see errors
"%~dp0bin\Debug\net8.0-windows\KokonoeAssistant.exe"

if errorlevel 1 (
    echo [ERROR] Додаток завершив з помилкою код %errorlevel%
    pause
)

endlocal
