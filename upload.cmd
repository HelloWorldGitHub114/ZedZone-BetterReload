@echo off
setlocal

set "UPLOADER=E:\Projects\ZedZoneMods\_tools\WorkshopUploader\bin\WorkshopUploader.exe"
set "VDF=%~dp0workshop_item.vdf"

if not exist "%UPLOADER%" (
    echo [ERROR] WorkshopUploader not found at: %UPLOADER%
    pause
    exit /b 1
)

if not exist "%VDF%" (
    echo [ERROR] VDF file not found at: %VDF%
    pause
    exit /b 1
)

echo === Step 1: Building BetterReload from source and staging content ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo === Step 2: Uploading via logged-in Steam Desktop Client ===
echo Make sure Steam is open and connected ^(no TUN mode or SteamCMD needed^).
"%UPLOADER%" "%VDF%"
set "EXIT_CODE=%ERRORLEVEL%"

pause
exit /b %EXIT_CODE%
