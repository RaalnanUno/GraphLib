

@echo off
REM ============================================
REM Run a SQL file against a SQLite database
REM run-sql.bat schema.sql
REM run-sql.bat update-settings.sql
REM ============================================

REM ---- CONFIG ----
set SQLITE_EXE=sqlite3
set DB_PATH=.\Data\GraphLib.db
set SQL_FILE=%1

REM ---- VALIDATION ----
if "%SQL_FILE%"=="" (
  echo Usage: run-sql.bat path\to\script.sql
  exit /b 1
)

if not exist "%DB_PATH%" (
  echo Database not found: %DB_PATH%
  exit /b 1
)

if not exist "%SQL_FILE%" (
  echo SQL file not found: %SQL_FILE%
  exit /b 1
)

REM ---- EXECUTE ----
echo Running %SQL_FILE% against %DB_PATH%
"%SQLITE_EXE%" "%DB_PATH%" < "%SQL_FILE%"

if errorlevel 1 (
  echo SQL execution failed.
  exit /b 1
)

echo Done.
