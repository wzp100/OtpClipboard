@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" %*
if errorlevel 1 (
    echo.
    echo Install failed. See the messages above.
    pause
    exit /b 1
)
timeout /t 5 >nul
