@echo off
setlocal

REM ==============================
REM CONFIG
REM ==============================

REM Source directory (current folder)
set SOURCE_DIR=%CD%

REM Destination directory (CHANGE THIS)
set DEST_DIR=C:\temp\VBWebForms_Copy

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
  *.vb *.aspx *.aspx.vb *.ascx *.ascx.vb *.master *.master.vb ^
  *.config *.csproj *.vbproj *.sln *.json *.xml *.resx *.md ^
  *.editorconfig *.props *.targets ^
  /S ^
  /XF *.user *.suo *.cache ^
  /XD bin obj .vs packages .git .vscode App_Data ^
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
