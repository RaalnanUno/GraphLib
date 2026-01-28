@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM GraphLib.PdfConsoleNet48 - One-shot build + run
REM Fill in the values below, then run:
REM   build-and-run.bat "C:\docs\example.docx" "C:\docs\example.pdf"
REM Output PDF is optional; if omitted, it writes next to input.
REM ============================================================

REM ---- REQUIRED SETTINGS (FILL THESE IN) ----------------------
set "TENANT_ID=YOUR-TENANT-ID"
set "CLIENT_ID=YOUR-CLIENT-ID"
set "CLIENT_SECRET=YOUR-CLIENT-SECRET"

set "SITE_URL=https://contoso.sharepoint.com/sites/MySite"
set "LIBRARY_NAME=Documents"
set "TEMP_FOLDER=_graphlib-temp"
set "CONFLICT_BEHAVIOR=replace"

REM ---- OPTIONAL SETTINGS --------------------------------------
REM Turn on silent SQLite error logging (requires Microsoft.Data.Sqlite package + uncommented logger code)
set "ENABLE_SQLITE_LOGGING=false"
REM Leave blank to use LocalAppData default
set "SQLITE_DB_PATH="

REM ---- PROJECT PATHS ------------------------------------------
set "CSPROJ=src\GraphLib.PdfConsoleNet48\GraphLib.PdfConsoleNet48.csproj"
set "CONFIG=Debug"

REM ---- INPUT/OUTPUT -------------------------------------------
if "%~1"=="" (
  echo Usage: %~nx0 ^<inputFile^> [outputPdf]
  echo Example: %~nx0 "C:\docs\example.docx" "C:\docs\example.pdf"
  exit /b 2
)

set "INPUT_FILE=%~1"
set "OUTPUT_PDF=%~2"

if not exist "%INPUT_FILE%" (
  echo ERROR: Input file not found: "%INPUT_FILE%"
  exit /b 2
)

REM If output not provided, default next to input.
if "%OUTPUT_PDF%"=="" (
  for %%F in ("%INPUT_FILE%") do (
    set "OUTPUT_PDF=%%~dpnF.pdf"
  )
)

REM ---- BASIC VALIDATION ---------------------------------------
call :RequireValue TENANT_ID "%TENANT_ID%"
call :RequireValue CLIENT_ID "%CLIENT_ID%"
call :RequireValue CLIENT_SECRET "%CLIENT_SECRET%"
call :RequireValue SITE_URL "%SITE_URL%"
call :RequireValue LIBRARY_NAME "%LIBRARY_NAME%"

REM Prevent common placeholder mistake
if /I "%TENANT_ID%"=="YOUR-TENANT-ID" (
  echo ERROR: TENANT_ID is still a placeholder.
  exit /b 2
)
if /I "%CLIENT_ID%"=="YOUR-CLIENT-ID" (
  echo ERROR: CLIENT_ID is still a placeholder.
  exit /b 2
)
if /I "%CLIENT_SECRET%"=="YOUR-CLIENT-SECRET" (
  echo ERROR: CLIENT_SECRET is still a placeholder.
  exit /b 2
)

REM ---- LOCATE MSBUILD -----------------------------------------
set "MSBUILD=msbuild"

REM If running from "Developer Command Prompt for VS", msbuild is already available.
REM Otherwise, try to find MSBuild via vswhere (VS 2017+).
where /q msbuild
if errorlevel 1 (
  if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%I in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
      set "MSBUILD=%%I"
    )
  )
)

if not exist "%MSBUILD%" (
  where msbuild >nul 2>nul
  if errorlevel 1 (
    echo ERROR: Could not find MSBuild.
    echo - Open "Developer Command Prompt for VS" and re-run, OR
    echo - Install Visual Studio Build Tools.
    exit /b 2
  )
)

echo ------------------------------------------------------------
echo Building GraphLib.PdfConsoleNet48 with MSBuild props...
echo Project : %CSPROJ%
echo Input   : "%INPUT_FILE%"
echo Output  : "%OUTPUT_PDF%"
echo ------------------------------------------------------------

REM ---- BUILD ---------------------------------------------------
"%MSBUILD%" "%CSPROJ%" ^
  /t:Restore;Build ^
  /p:Configuration=%CONFIG% ^
  /p:TenantId="%TENANT_ID%" ^
  /p:ClientId="%CLIENT_ID%" ^
  /p:ClientSecret="%CLIENT_SECRET%" ^
  /p:SiteUrl="%SITE_URL%" ^
  /p:LibraryName="%LIBRARY_NAME%" ^
  /p:TempFolder="%TEMP_FOLDER%" ^
  /p:ConflictBehavior="%CONFLICT_BEHAVIOR%" ^
  /p:EnableSqliteLogging="%ENABLE_SQLITE_LOGGING%" ^
  /p:SqliteDbPath="%SQLITE_DB_PATH%" ^
  /v:minimal

if errorlevel 1 (
  echo.
  echo ERROR: Build failed.
  exit /b 1
)

REM ---- RUN -----------------------------------------------------
REM The exe path depends on the SDK-style net48 output folder.
set "EXE=src\GraphLib.PdfConsoleNet48\bin\%CONFIG%\net48\GraphLib.PdfConsoleNet48.exe"

if not exist "%EXE%" (
  echo ERROR: Expected exe not found: "%EXE%"
  echo If your output path differs, update EXE in this BAT.
  exit /b 1
)

echo.
echo ------------------------------------------------------------
echo Running...
echo ------------------------------------------------------------
"%EXE%" "%INPUT_FILE%" "%OUTPUT_PDF%"

set "RC=%ERRORLEVEL%"

echo.
echo ------------------------------------------------------------
if "%RC%"=="0" (
  echo OK - PDF generated: "%OUTPUT_PDF%"
) else (
  echo FAIL - ExitCode=%RC%
)
echo ------------------------------------------------------------

exit /b %RC%

REM ============================================================
REM Helpers
REM ============================================================
:RequireValue
REM %1 = name, %2 = value
set "N=%~1"
set "V=%~2"
if "%V%"=="" (
  echo ERROR: %N% is empty.
  exit /b 2
)
exit /b 0
