@echo off
setlocal enabledelayedexpansion
rem Builds "Test Computer System.exe" with the C# compiler that ships with Windows (no install needed).
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

rem embed every language file (lang\*.lang) and the legal text
set RES=/resource:lang\legal.txt,legal.txt
for %%f in (lang\*.lang) do set RES=!RES! /resource:%%f,lang.%%~nf.lang

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /debug- ^
  /win32manifest:app.manifest /win32icon:app.ico ^
  /out:"Test Computer System.exe" !RES! ^
  /r:System.dll /r:System.Core.dll /r:System.Management.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\*.cs

if errorlevel 1 (echo. & echo BUILD FAILED) else (echo. & echo BUILD OK: Test Computer System.exe)
pause
