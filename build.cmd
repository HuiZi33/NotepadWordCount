@echo off
rem Build with the C# compiler bundled with Windows. No SDK, no download.
rem Source: src\*.cs   Output: dist\<name>.exe
setlocal enabledelayedexpansion

cd /d "%~dp0"

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
  pause
  exit /b 1
)

set SRC=
for %%f in ("src\*.cs") do set SRC=%%~nxf
if "%SRC%"=="" (
  echo [ERROR] no .cs file in src\
  pause
  exit /b 1
)

set NAME=%SRC:.cs=%
if not exist dist mkdir dist

echo Building dist\%NAME%.exe from src\%SRC% ...
"%CSC%" /nologo /target:winexe /optimize+ /out:"dist\%NAME%.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll "src\%SRC%"

if errorlevel 1 (
  echo [ERROR] build failed
) else (
  echo [OK] dist\%NAME%.exe
)
pause
