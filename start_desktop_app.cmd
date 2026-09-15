@echo off
rem Double-clickable launcher for start_desktop_app.ps1.
rem
rem The .ps1 is the real script; this wrapper only exists so the app can be
rem started from Explorer or a shortcut without fighting the execution policy.
rem Any arguments are forwarded (e.g. -SkipBuild, -Port 8080).
rem
rem The console window this opens is intentional and stays for the app's
rem lifetime: the wrapper app is OutputType=Exe and echoes every server log line
rem to stdout, so this window is where the log shows up.
setlocal

set "SCRIPT=%~dp0start_desktop_app.ps1"

where pwsh >nul 2>nul
if %ERRORLEVEL% equ 0 (
    pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
) else (
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
)

set "RC=%ERRORLEVEL%"

rem A double-click closes this console the moment the script ends, taking a build
rem error with it, so give the error some time on screen when something went
rem wrong. `timeout` rather than `pause` on purpose: it refuses to wait when stdin
rem is redirected (a caller piping output, or a job with no console), so this can
rem never block an automated run - and it is bounded even in the worst case.
if not "%RC%"=="0" (
    echo.
    echo start_desktop_app failed with exit code %RC%.
    timeout /t 15 >nul
)

exit /b %RC%
