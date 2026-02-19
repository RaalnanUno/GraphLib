@echo off
setlocal

REM ==============================
REM CONFIG
REM ==============================

REM Source directory (current folder)
set SOURCE_DIR=%CD%

REM Destination directory (CHANGE THIS)
set DEST_DIR=C:\temp\HearthOwlCS_Copy

REM ==============================
REM CREATE DEST IF NEEDED
REM ==============================

if not exist "%DEST_DIR%" (
    mkdir "%DEST_DIR%"
)

REM ==============================
REM COPY FILES
REM ==============================

robocopy "%SOURCE_DIR%" "%DEST_DIR%" ^
  *.cs *.csproj *.sln *.config *.json *.md *.editorconfig *.resx *.props *.targets ^
  /S ^
  /XF *.user *.suo ^
  /XD bin obj .vs packages .git .vscode ^
  /R:1 /W:1 ^
  /NFL /NDL

REM ==============================
REM DONE
REM ==============================

echo.
echo Copy complete.
echo Source: %SOURCE_DIR%
echo Dest:   %DEST_DIR%
echo.

endlocal
pause
