@echo off
setlocal
set "REMOVE_CERT="
choice /c YN /n /m "Also remove the test signing certificate? [Y/N] "
if errorlevel 2 goto run
set "REMOVE_CERT=-RemoveCertificate"
:run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1" %REMOVE_CERT%
echo.
pause
