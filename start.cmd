@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE="
if exist "soba-desk.exe" set "EXE=%cd%\soba-desk.exe"
if not defined EXE if exist "dist\soba-desk.exe" set "EXE=%cd%\dist\soba-desk.exe"
if not defined EXE (
  echo soba-desk.exe is missing.
  echo Publish with .NET 8 SDK:
  echo   dotnet publish src\SobaDesk\SobaDesk.csproj -c Release -r win-x64 --self-contained true -o dist
  pause
  exit /b 1
)
if not "%~1"=="" (
  start "" "%EXE%" "%~1"
) else (
  start "" "%EXE%"
)
