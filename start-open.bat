@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE="
if exist "soba-desk.exe" set "EXE=%cd%\soba-desk.exe"
if not defined EXE if exist "dist\soba-desk.exe" set "EXE=%cd%\dist\soba-desk.exe"
if not defined EXE (
  echo soba-desk.exe is missing.
  pause
  exit /b 1
)
if not "%~1"=="" (
  start "" "%EXE%" --open "%~1"
) else (
  start "" "%EXE%" --open
)
