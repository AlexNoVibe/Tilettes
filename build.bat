@echo off
rem Builds Tilettes as two native-platform exes: Tilettes_x86.exe (32-bit,
rem runs on any Windows, including 32-bit ones) and Tilettes_x64.exe (64-bit).
rem The GitHub Actions release workflow builds and ships exactly these two
rem files.
setlocal
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x86 /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes_x86.exe src\*.cs
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x64 /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:Tilettes_x64.exe src\*.cs
endlocal
