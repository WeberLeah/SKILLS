@echo off
setlocal
rem ============================================================
rem  Edit SERVER to the IP of the PC running TestNotify.exe
rem  Usage:  send.bat PASS [station] [sn] [message]
rem  Example:send.bat FAIL A1 SN001 "voltage too high"
rem ============================================================
set "SERVER=192.0.2.10"
set "PORT=8080"

if "%~1"=="" (
  echo Usage: send.bat ^<PASS^|FAIL^> [station] [sn] [message]
  echo Example: send.bat FAIL A1 SN001 "voltage too high"
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Send-TestResult.ps1" ^
  -Server "%SERVER%" -Port %PORT% -Result "%~1" -Station "%~2" -SN "%~3" -Message "%~4"
