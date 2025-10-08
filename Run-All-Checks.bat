@echo off
REM =============================================================================
REM Win11Forge - Run All Checks (Analyzer, Version, Validation, Tests)
REM =============================================================================

setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

REM Detect PowerShell (prefer pwsh if available)
set "PS_EXEC=PowerShell.exe"

REM Try pwsh.exe in PATH
pwsh.exe -NoProfile -Command "exit 0" >nul 2>&1
if %errorlevel% equ 0 (
    set "PS_EXEC=C:\Program Files\PowerShell\7\pwsh.exe"
    goto :ps_found
)

REM Try common pwsh installation paths
if exist "C:\Program Files\PowerShell\7\pwsh.exe" (
    set "PS_EXEC=C:\Program Files\PowerShell\7\pwsh.exe"
    goto :ps_found
)

if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" (
    set "PS_EXEC=%ProgramFiles%\PowerShell\7\pwsh.exe"
    goto :ps_found
)

:ps_found

echo.
echo ============================================================================
echo                       Win11Forge - Run All Checks
echo ============================================================================
echo   Using: %PS_EXEC%
echo   Root : %SCRIPT_DIR%
echo ============================================================================
echo.

set "FAIL=0"

REM --- 1) Version Consistency --------------------------------------------------
set "VER_SCRIPT=%SCRIPT_DIR%\Tools\Verify-VersionConsistency.ps1"
if exist "%VER_SCRIPT%" (
  echo [STEP] Version consistency check...
  "%PS_EXEC%" -NoProfile -ExecutionPolicy Bypass -File "%VER_SCRIPT%"
  if errorlevel 1 (
    echo [FAIL] Version consistency issues detected
    set "FAIL=1"
  ) else (
    echo [OK]   Version consistency passed
  )
) else (
  echo [SKIP] Version check script not found
)
echo.

REM --- 2) PSScriptAnalyzer -----------------------------------------------------
set "ANALYZE_SCRIPT=%SCRIPT_DIR%\Tools\Invoke-PSScriptAnalyzer.ps1"
if exist "%ANALYZE_SCRIPT%" (
  echo [STEP] Static analysis PSScriptAnalyzer...
  "%PS_EXEC%" -NoProfile -ExecutionPolicy Bypass -File "%ANALYZE_SCRIPT%"
  if errorlevel 1 (
    echo [WARN] Analyzer reported issues
  ) else (
    echo [OK]   Analyzer completed without fatal errors
  )
) else (
  echo [SKIP] Analyzer script not found
)
echo.

REM --- 3) Framework Validation -------------------------------------------------
set "VALIDATE_SCRIPT=%SCRIPT_DIR%\Tools\Validate-Framework.ps1"
if exist "%VALIDATE_SCRIPT%" (
  echo [STEP] Framework validation...
  "%PS_EXEC%" -NoProfile -ExecutionPolicy Bypass -File "%VALIDATE_SCRIPT%" -Detailed
  if errorlevel 1 (
    echo [FAIL] Framework validation failed
    set "FAIL=1"
  ) else (
    echo [OK]   Framework validation passed
  )
) else (
  echo [SKIP] Validation script not found
)
echo.

REM --- 4) Database Validation --------------------------------------------------
set "DB_VALIDATE=%SCRIPT_DIR%\Tools\Validate-AppDatabase.ps1"
if exist "%DB_VALIDATE%" (
  echo [STEP] Application database validation...
  "%PS_EXEC%" -NoProfile -ExecutionPolicy Bypass -File "%DB_VALIDATE%"
  if errorlevel 1 (
    echo [FAIL] App database validation reported issues
    set "FAIL=1"
  ) else (
    echo [OK]   App database validation passed
  )
) else (
  echo [SKIP] App database validation script not found
)
echo.

REM --- 5) Tests Pester ---------------------------------------------------------
set "TEST_RUNNER=%SCRIPT_DIR%\Tests\Invoke-Tests.ps1"
if exist "%TEST_RUNNER%" (
  echo [STEP] Running Pester tests...
  "%PS_EXEC%" -NoProfile -ExecutionPolicy Bypass -File "%TEST_RUNNER%"
  if errorlevel 1 (
    echo [FAIL] Tests failed
    set "FAIL=1"
  ) else (
    echo [OK]   All tests passed
  )
) else (
  echo [SKIP] Test runner not found
)
echo.

echo ============================================================================
if "%FAIL%"=="0" (
  echo [DONE] All checks completed successfully
  exit /b 0
) else (
  echo [DONE] Some checks failed - see details above
  exit /b 1
)