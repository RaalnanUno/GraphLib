@echo off
setlocal EnableExtensions EnableDelayedExpansion

echo Generating markdown snapshots and prompt index...
echo.

REM ================================================
REM 1) Copy specific root files to .md (if present)
REM ================================================
for %%F in (
  "KeyFolio.Client.csproj"
    "Form1.cs"
  "Program.cs"
) do (
  if exist "%%~F" (
    copy /y "%%~F" "%%~dpnF.md" >nul
    echo Copied root file: %%~F → %%~dpnF.md
  )
)

REM =======================================================
REM 2) Copy ALL .cs/.csx under src to corresponding .md
REM =======================================================
for /r "src" %%F in (*.cs) do (
  copy /y "%%~fF" "%%~dpnF.md" >nul
  echo Copied: %%~fF → %%~dpnF.md
)

REM =======================================================
REM 2) Copy ALL .cs/.csx under src to corresponding .md
REM =======================================================
for /r "src" %%F in (*.html) do (
  copy /y "%%~fF" "%%~dpnF.md" >nul
  echo Copied: %%~fF → %%~dpnF.md
)
for /r "src" %%F in (*.css) do (
  copy /y "%%~fF" "%%~dpnF.md" >nul
  echo Copied: %%~fF → %%~dpnF.md
)
for /r "src" %%F in (*.js) do (
  copy /y "%%~fF" "%%~dpnF.md" >nul
  echo Copied: %%~fF → %%~dpnF.md
)

for /r "src" %%F in (*.sql) do (
  copy /y "%%~fF" "%%~dpnF.md" >nul
  echo Copied: %%~fF → %%~dpnF.md
)

REM =======================================================
REM 3) Build prompt.md with path headers and relative links
REM =======================================================
set "ROOT=%CD%"
if exist "prompt.md" (
  del "prompt.md"
)

echo.
echo Building prompt.md index...
echo.

REM 3a) Root-level md files we generated
for %%F in (
  "KeyFolio.Client.md"
    "Form1.md"
  "Program.md"
) do (
  if exist "%%~F" (
    >>"prompt.md" echo ## %%~nxF
    >>"prompt.md" echo.
    >>"prompt.md" echo ^^![[%%~nxF]]
    >>"prompt.md" echo.
    echo Indexed root: %%~nxF
  )
)

REM 3b) All md files under src/** (includes subdirs)
for /r "src" %%F in (*.md) do (
  if /I not "%%~nxF"=="prompt.md" (
    set "abs=%%~fF"
    set "rel=!abs:%ROOT%\=!"
    set "rel=!rel:\=/!"
    set "orig=!rel:.md=.cs!"
    >>"prompt.md" echo ## !orig!
    >>"prompt.md" echo.
    >>"prompt.md" echo ^^![[!rel!]]
    >>"prompt.md" echo.
    echo Indexed: !orig!
  )
)
REM 3b) All md files under src/** (includes subdirs)
for /r "src" %%F in (*.md) do (
  if /I not "%%~nxF"=="prompt.md" (
    set "abs=%%~fF"
    set "rel=!abs:%ROOT%\=!"
    set "rel=!rel:\=/!"
    set "orig=!rel:.md=.cs!"
    >>"prompt.md" echo ## !orig!
    >>"prompt.md" echo.
    >>"prompt.md" echo ^^![[!rel!]]
    >>"prompt.md" echo.
    echo Indexed: !orig!
  )
)


echo.
echo ========================================
echo Finished!
echo Created markdown snapshots and prompt.md
echo ========================================

endlocal
pause
