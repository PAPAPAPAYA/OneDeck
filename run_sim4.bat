@echo off
setlocal
chcp 65001 >nul
rem ============================================================
rem  OneDeck 4.0 simulator runner.
rem  Keep this file in the repo root. For desktop use, create a
rem  SHORTCUT to it (do not copy the file itself - it locates the
rem  repo relative to its own path).
rem ============================================================

set "SCRIPTS=%~dp0tools\scripts\"
set "OUTDIR=%~dp0tools\outputs\sim4"

where python >nul 2>nul
if errorlevel 1 (
    echo [ERROR] python not found on PATH.
    pause
    exit /b 1
)

echo ================================================
echo   OneDeck 4.0 simulator
echo ================================================
echo   [1] Full reports  (4 configs x 8 batches, ~1 min)
echo   [2] Selftest only (engine health check)
echo   [3] Dump card table (prefab card table JSON)
echo ================================================
set "CHOICE="
set /p CHOICE=Select [1/2/3, default 1]:
if "%CHOICE%"=="" set CHOICE=1

if "%CHOICE%"=="2" goto selftest
if "%CHOICE%"=="3" goto dump

set "SESSIONS=200"
set /p INPUT=Sessions per batch (default 200):
if not "%INPUT%"=="" set "SESSIONS=%INPUT%"

echo.
echo [1/2] selftest...
pushd "%SCRIPTS%"
python one_deck_damage_sim.py --selftest-40
if errorlevel 1 goto fail
echo.
echo [2/2] generating reports (%SESSIONS% sessions per batch)...
python one_deck_damage_sim.py --report-40 --sessions %SESSIONS%
if errorlevel 1 goto fail
popd
echo.
echo Done. Reports in: %OUTDIR%
start "" explorer "%OUTDIR%"
pause
exit /b 0

:selftest
pushd "%SCRIPTS%"
python one_deck_damage_sim.py --selftest-40
set "RC=%errorlevel%"
popd
echo.
pause
exit /b %RC%

:dump
pushd "%SCRIPTS%"
python one_deck_damage_sim.py --dump-pool trial40
popd
echo Table: %OUTDIR%\prefab_card_table_trial.json
pause
exit /b 0

:fail
popd
echo.
echo [ERROR] run failed, see messages above.
pause
exit /b 1
