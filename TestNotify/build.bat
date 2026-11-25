@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. Requires .NET Framework 4.x, built into Windows.
  pause
  exit /b 1
)

echo Building receiver  -^> TestNotify.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:TestNotify.exe Receiver.cs
if errorlevel 1 goto fail

echo Building sender    -^> Sender.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:Sender.exe Sender.cs
if errorlevel 1 goto fail

echo.
echo Build OK: TestNotify.exe + Sender.exe
echo NOTE: dashboard.html must stay next to TestNotify.exe
exit /b 0

:fail
echo.
echo [ERROR] build failed.
pause
exit /b 1
