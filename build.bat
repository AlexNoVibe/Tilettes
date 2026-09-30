@echo off
rem Usage: build.bat [x86|x64] - without an argument this builds the
rem universal AnyCPU Tilettes.exe; with one it builds Tilettes-<platform>.exe
rem (used by the GitHub Actions release workflow to attach per-CPU builds).
setlocal
set OUT=Tilettes.exe
set PLATSW=
if /i "%~1"=="x86" (set OUT=Tilettes-x86.exe& set PLATSW=/platform:x86)
if /i "%~1"=="x64" (set OUT=Tilettes-x64.exe& set PLATSW=/platform:x64)
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ %PLATSW% /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:%OUT% src\*.cs
endlocal
