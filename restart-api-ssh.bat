@echo off
setlocal EnableExtensions
call :RestartApi %*
set "RESTART_RESULT=%ERRORLEVEL%"
echo.
if "%RESTART_RESULT%"=="0" (
  echo SUCCESS: API service restarted and is active.
) else (
  echo FAILED: API restart did not complete. Exit code: %RESTART_RESULT%
  echo Review the messages above before closing this window.
)
echo.
pause
exit /b %RESTART_RESULT%

:RestartApi
cd /d "%~dp0" || exit /b 1

rem Manual restart of the production systemd service.
rem SSH asks for the amnote password; su then asks for the root password.
rem API environment and port are managed on the server in /etc/amnote/api.env.
if not "%~1"=="" (
  echo This script takes no arguments. Configure URLs on the server.
  exit /b 1
)

where ssh.exe >nul 2>&1
if errorlevel 1 (
  echo OpenSSH ssh.exe not found.
  exit /b 1
)

echo Restarting amnote-api.service on 118.69.170.82.
echo Enter the SSH password, then the root password when prompted.
ssh.exe -t -o StrictHostKeyChecking=yes -o ConnectTimeout=15 -p 22 amnote@118.69.170.82 "su - root -c 'systemctl restart amnote-api.service && systemctl is-active amnote-api.service'"
if errorlevel 1 (
  echo Restart failed. Check the SSH output above.
  exit /b 1
)
echo API service is active.
exit /b 0
