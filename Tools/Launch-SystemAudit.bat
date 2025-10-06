@echo off
REM ============================================================================
REM  Launch-SystemAudit.bat
REM  Automatic privilege elevation for System-Audit.ps1
REM
REM  Author: Julien Bombled
REM  Version: 1.0.0
REM  Date: 2025-10-06
REM ============================================================================

title System-Audit Tool Launcher

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    goto :RunScript
) else (
    goto :ElevatePrivileges
)

:ElevatePrivileges
echo.
echo ========================================
echo   Elevation Required
echo ========================================
echo.
echo This script requires Administrator privileges.
echo Requesting elevation...
echo.

REM Re-launch this script with administrator privileges
powershell -Command "Start-Process '%~f0' -Verb RunAs"
exit /b

:RunScript
echo.
echo ========================================
echo   System-Audit Tool Launcher
echo ========================================
echo.
echo Running with Administrator privileges...
echo.

REM Get the directory where this bat file is located
set "SCRIPT_DIR=%~dp0"

REM Navigate to script directory
cd /d "%SCRIPT_DIR%"

REM Display menu
:Menu
cls
echo.
echo ============================================================
echo   System-Audit.ps1 - Quick Launch Menu
echo ============================================================
echo.
echo   Select monitoring mode:
echo.
echo   1. Win11Forge Deployment (auto-stop on completion)
echo   2. Monitor Process by Name
echo   3. Monitor Process by PID
echo   4. Monitor Log File
echo   5. Monitor Log Directory
echo   6. Timed Audit (30 minutes)
echo   7. Custom Parameters (advanced)
echo   8. Launch with PowerShell ISE (for editing)
echo.
echo   0. Exit
echo.
echo ============================================================
echo.

set /p choice="Enter your choice (0-8): "

if "%choice%"=="1" goto :Win11ForgeMode
if "%choice%"=="2" goto :ProcessNameMode
if "%choice%"=="3" goto :ProcessIdMode
if "%choice%"=="4" goto :LogFileMode
if "%choice%"=="5" goto :LogDirMode
if "%choice%"=="6" goto :TimedMode
if "%choice%"=="7" goto :CustomMode
if "%choice%"=="8" goto :ISEMode
if "%choice%"=="0" goto :Exit

echo Invalid choice. Please try again.
timeout /t 2 >nul
goto :Menu

REM ============================================================
REM   Mode 1: Win11Forge Deployment
REM ============================================================
:Win11ForgeMode
cls
echo.
echo ============================================================
echo   Win11Forge Deployment Monitoring
echo ============================================================
echo.
echo This will monitor the Logs directory and auto-stop when
echo deployment completes.
echo.
echo Report will be generated automatically.
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -MonitorLogPath "%SCRIPT_DIR%..\Logs" -LogCompletionMarkers "Deployment completed|Summary" -GenerateReport -AuditName "Win11ForgeDeployment"
goto :End

REM ============================================================
REM   Mode 2: Monitor Process by Name
REM ============================================================
:ProcessNameMode
cls
echo.
echo ============================================================
echo   Monitor Process by Name
REM ============================================================
echo.
set /p processName="Enter process name (e.g., powershell, winget): "
set /p auditName="Enter audit name (default: ProcessMonitor): "

if "%auditName%"=="" set auditName=ProcessMonitor

echo.
echo Monitoring process: %processName%
echo Audit name: %auditName%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -MonitorProcessName "%processName%" -AuditName "%auditName%" -GenerateReport
goto :End

REM ============================================================
REM   Mode 3: Monitor Process by PID
REM ============================================================
:ProcessIdMode
cls
echo.
echo ============================================================
echo   Monitor Process by PID
echo ============================================================
echo.
echo First, you need to start your process and get its PID.
echo.
echo Opening Task Manager to find PID...
echo (Go to Details tab and note the PID)
echo.
pause
start taskmgr.exe

echo.
set /p processPid="Enter process PID: "
set /p auditName="Enter audit name (default: PIDMonitor): "

if "%auditName%"=="" set auditName=PIDMonitor

echo.
echo Monitoring PID: %processPid%
echo Audit name: %auditName%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -MonitorProcessId %processPid% -AuditName "%auditName%" -GenerateReport
goto :End

REM ============================================================
REM   Mode 4: Monitor Log File
REM ============================================================
:LogFileMode
cls
echo.
echo ============================================================
echo   Monitor Log File
echo ============================================================
echo.
set /p logFile="Enter full path to log file: "
set /p markers="Enter completion markers (regex, default: completed^|finished): "
set /p auditName="Enter audit name (default: LogFileMonitor): "

if "%markers%"=="" set markers=completed^|finished
if "%auditName%"=="" set auditName=LogFileMonitor

echo.
echo Monitoring log: %logFile%
echo Completion markers: %markers%
echo Audit name: %auditName%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -MonitorLogFile "%logFile%" -LogCompletionMarkers "%markers%" -AuditName "%auditName%" -GenerateReport
goto :End

REM ============================================================
REM   Mode 5: Monitor Log Directory
REM ============================================================
:LogDirMode
cls
echo.
echo ============================================================
echo   Monitor Log Directory
echo ============================================================
echo.
set /p logDir="Enter path to log directory: "
set /p markers="Enter completion markers (regex, default: completed^|finished): "
set /p auditName="Enter audit name (default: LogDirMonitor): "

if "%markers%"=="" set markers=completed^|finished
if "%auditName%"=="" set auditName=LogDirMonitor

echo.
echo Monitoring directory: %logDir%
echo Completion markers: %markers%
echo Audit name: %auditName%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -MonitorLogPath "%logDir%" -LogCompletionMarkers "%markers%" -AuditName "%auditName%" -GenerateReport
goto :End

REM ============================================================
REM   Mode 6: Timed Audit
REM ============================================================
:TimedMode
cls
echo.
echo ============================================================
echo   Timed Audit
echo ============================================================
echo.
echo This will run a 30-minute system audit without auto-stop.
echo.
set /p auditName="Enter audit name (default: TimedAudit): "

if "%auditName%"=="" set auditName=TimedAudit

echo.
echo Duration: 30 minutes
echo Audit name: %auditName%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" -Duration 30 -AuditName "%auditName%" -GenerateReport
goto :End

REM ============================================================
REM   Mode 7: Custom Parameters
REM ============================================================
:CustomMode
cls
echo.
echo ============================================================
echo   Custom Parameters Mode
echo ============================================================
echo.
echo Enter custom PowerShell parameters for System-Audit.ps1
echo.
echo Examples:
echo   -MonitorProcessName "winget" -SampleInterval 5
echo   -Duration 60 -MonitorRegistry -MonitorFileSystem
echo.
set /p customParams="Enter parameters: "

echo.
echo Launching with parameters: %customParams%
echo.
pause

powershell.exe -NoExit -ExecutionPolicy Bypass -File "%SCRIPT_DIR%System-Audit.ps1" %customParams%
goto :End

REM ============================================================
REM   Mode 8: PowerShell ISE
REM ============================================================
:ISEMode
cls
echo.
echo ============================================================
echo   Launch with PowerShell ISE
echo ============================================================
echo.
echo Opening System-Audit.ps1 in PowerShell ISE for editing...
echo.
pause

powershell.exe -Command "& {powershell_ise.exe '%SCRIPT_DIR%System-Audit.ps1'}"
goto :Menu

REM ============================================================
REM   Exit
REM ============================================================
:Exit
echo.
echo Exiting...
timeout /t 1 >nul
exit /b 0

:End
echo.
echo ============================================================
echo   Audit Complete
echo ============================================================
echo.
echo Press any key to return to menu or close this window...
pause >nul
goto :Menu
