@echo off
rem Builds the universal AnyCPU Tilettes.exe: it runs as a 64-bit process on
rem 64-bit Windows and as a 32-bit one on 32-bit Windows, so one file covers
rem every CPU (the GitHub Actions release workflow ships exactly this exe).
setlocal
set OUT=Tilettes.exe
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /win32icon:app.ico /win32manifest:app.manifest /keyfile:Tilettes.snk /out:%OUT% src\*.cs
endlocal
