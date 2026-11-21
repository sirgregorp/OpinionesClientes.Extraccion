@echo off
rem Ejecuta scripts\demo-completa.ps1 (doble clic desde el Explorador de archivos)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0demo-completa.ps1" %*
echo.
pause
