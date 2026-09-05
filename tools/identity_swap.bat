@echo off
setlocal
rem ============================================================
rem identity_swap.bat - swap the async-PvP identity file between
rem named account slots, for local multi-account testing.
rem
rem Usage (run from anywhere):
rem   tools\identity_swap.bat save <slot>   archive current identity as <slot>
rem   tools\identity_swap.bat use <slot>    swap current identity with <slot>
rem   tools\identity_swap.bat list          list slots with their usernames
rem   tools\identity_swap.bat show          print current identity file
rem
rem DATADIR points at the EDITOR end by default (ProjectSettings:
rem companyName=SmallGrass, productName=OneDeck). For an isolated "-B"
rem build (see Assets/Scripts/Editor/BuildIsolatedDataDir.cs) point
rem DATADIR at %LOCALAPPDATA%Low\SmallGrass\OneDeck-B instead, or set
rem IDENTITY_SWAP_DATADIR for one-off runs. Restart the game after a swap.
rem ============================================================

set "COMPANY=SmallGrass"
set "PRODUCT=OneDeck"
set "DATADIR=%LOCALAPPDATA%Low\%COMPANY%\%PRODUCT%"
if defined IDENTITY_SWAP_DATADIR set "DATADIR=%IDENTITY_SWAP_DATADIR%"
set "CURRENT=%DATADIR%\player_identity.json"

if "%~1"=="" goto usage
if /i "%~1"=="show" goto show
if /i "%~1"=="list" goto list
if /i "%~1"=="save" goto save
if /i "%~1"=="use" goto use
goto usage

:show
if not exist "%CURRENT%" (
	echo No identity file yet: "%CURRENT%"
	exit /b 1
)
echo [%CURRENT%]
type "%CURRENT%"
echo.
exit /b 0

:list
echo Slots in %DATADIR%:
dir /b "%DATADIR%\player_identity.*.json" 2>nul
if errorlevel 1 echo   (none yet - register in game once, then run: identity_swap.bat save A)
exit /b 0

:save
if "%~2"=="" (
	echo Usage: identity_swap.bat save ^<slot^>
	exit /b 1
)
if not exist "%CURRENT%" (
	echo No current identity to save: "%CURRENT%"
	exit /b 1
)
set "SLOT=%DATADIR%\player_identity.%~2.json"
copy /y "%CURRENT%" "%SLOT%" >nul
echo Saved current identity as slot "%~2": %SLOT%
exit /b 0

:use
if "%~2"=="" (
	echo Usage: identity_swap.bat use ^<slot^>
	exit /b 1
)
if not exist "%DATADIR%" (
	echo Data dir missing: "%DATADIR%"
	exit /b 1
)
set "SLOT=%DATADIR%\player_identity.%~2.json"
if not exist "%SLOT%" (
	echo Slot "%~2" does not exist. Register the new account in game once, then: identity_swap.bat save %~2
	exit /b 1
)
if not exist "%CURRENT%" (
	echo No current identity; adopting slot "%~2" as current.
	copy /y "%SLOT%" "%CURRENT%" >nul
	exit /b 0
)
rem True swap: the latest state of both accounts is preserved for the next switch.
copy /y "%CURRENT%" "%DATADIR%\player_identity.swap.tmp" >nul
copy /y "%SLOT%" "%CURRENT%" >nul
move /y "%DATADIR%\player_identity.swap.tmp" "%SLOT%" >nul
echo Swapped with slot "%~2". Current identity is now:
type "%CURRENT%"
echo.
exit /b 0

:usage
echo Usage: identity_swap.bat save ^<slot^> ^| use ^<slot^> ^| list ^| show
exit /b 1
