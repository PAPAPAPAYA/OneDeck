@echo off
setlocal
rem ============================================================
rem check_consistency.bat - one-command three-way card report:
rem engine prefabs (Assets/Prefabs/Cards/4.0) vs the Notion 4.0
rem card database vs the server card_catalog.
rem
rem Refreshes all three inputs first, then writes the HTML report
rem to tools\outputs\consistency_report_<date>.html and opens it.
rem
rem Usage (run from anywhere; double-click works too):
rem   tools\check_consistency.bat          refresh from prod, then check
rem   tools\check_consistency.bat -local   read the local dev DB instead
rem   tools\check_consistency.bat -cached  skip the refresh, check the dumps on disk
rem
rem Checker exit codes: 0 = clean, 1 = drift found (report written),
rem 2 = a refresh step failed (report NOT rewritten).
rem Needs python 3 and node on PATH; prod mode also needs the
rem workbench CLI in ~/.workbench/bin. Manual: docs\ConsistencyTool.md
rem ============================================================

rem Every path inside the python/node scripts is relative to the repo root.
cd /d "%~dp0.." || goto nodir

rem The scripts write UTF-8 and report in Chinese; the default Windows console
rem codepage (GBK) renders those lines as mojibake. Switch to UTF-8 for the run
rem and hand the console back the way we found it.
for /f "tokens=2 delims=:" %%c in ('chcp') do set "OLDCP=%%c"
chcp 65001 >nul

set "PROD=--prod"
set "REFRESH=--refresh"
if /i "%~1"=="-local" set "PROD="
if /i "%~1"=="-cached" set "REFRESH="

python tools\check_consistency.py %REFRESH% %PROD%
set "RC=%ERRORLEVEL%"

if "%RC%"=="2" goto refreshfailed

set "LATEST="
for /f "delims=" %%f in ('dir /b /o-d "tools\outputs\consistency_report_*.html" 2^>nul') do if not defined LATEST set "LATEST=%%f"
if not defined LATEST goto noreport
echo.
echo opening %LATEST%
start "" "tools\outputs\%LATEST%"
goto done

:refreshfailed
echo.
echo A refresh step failed - the report was NOT rewritten. Fix the step above
echo and run again; known pitfalls are in docs\ConsistencyTool.md.
echo To check the dumps already on disk instead, run:
echo   tools\check_consistency.bat -cached
goto done

:noreport
echo.
echo No report file found in tools\outputs.
goto done

:nodir
echo Could not enter the repo root from "%~dp0".

:done
echo.
if "%RC%"=="0" (
	echo Result: clean - no ERROR-level drift.
) else if "%RC%"=="1" (
	echo Result: ERROR-level drift found - see the report.
) else (
	echo Result: checker exited %RC% - see the output above.
)
if defined OLDCP chcp%OLDCP% >nul
pause
