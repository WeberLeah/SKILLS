@echo off
setlocal
net session >nul 2>&1
if errorlevel 1 (
  echo Please right-click this file and choose "Run as administrator".
  pause
  exit /b 1
)
netsh advfirewall firewall delete rule name="TestNotify 8080" >nul 2>&1
netsh advfirewall firewall add rule name="TestNotify 8080" dir=in action=allow protocol=TCP localport=8080 profile=private,domain
if errorlevel 1 (
  echo [ERROR] failed to add firewall rule.
) else (
  echo [OK] Firewall rule added: TCP 8080 for private/domain networks.
)
pause
