@echo off
setlocal enabledelayedexpansion
title Meta Quest VR App Installer ^& Fixer

echo ===================================================================
echo               Meta Quest VR App Installer ^& Fixer
echo ===================================================================
echo.

set ADB_EXE=""

if exist "C:\Users\%USERNAME%\AppData\Local\Programs\SideQuest\resources\platform-tools\adb.exe" (
    set ADB_EXE="C:\Users\%USERNAME%\AppData\Local\Programs\SideQuest\resources\platform-tools\adb.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" (
    set ADB_EXE="C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
) else (
    where adb >nul 2>&1
    if !errorlevel! equ 0 (
        set ADB_EXE=adb
    )
)

if %ADB_EXE%=="" (
    echo [ERROR] adb.exe could not be found.
    echo Please make sure SideQuest or Unity Android SDK is installed.
    echo.
    pause
    exit /b 1
)

echo [1/4] Checking connected Meta Quest device...
%ADB_EXE% devices
echo.

echo [2/4] Uninstalling old/conflicting package to prevent signature errors...
%ADB_EXE% uninstall com.DefaultCompany.stationwalkthrough >nul 2>&1
echo       Previous version cleaned.
echo.

set TARGET_APK=""

:: Check common APK locations
if exist "%~dp0build\VRBuild.apk" set TARGET_APK="%~dp0build\VRBuild.apk"
if exist "%~dp0build\StationWalkthrough.apk" set TARGET_APK="%~dp0build\StationWalkthrough.apk"
if exist "C:\Unity Projects\StationWalkthrough Builds\VRBuild7_new-1.apk" set TARGET_APK="C:\Unity Projects\StationWalkthrough Builds\VRBuild7_new-1.apk"

if "%~1"=="" (
    if !TARGET_APK!=="" (
        echo Found APK: !TARGET_APK!
        set /p CONFIRM="Install this APK? (Y/N) [Default Y]: "
        if /i "!CONFIRM!"=="N" (
            set TARGET_APK=""
        )
    )
) else (
    set TARGET_APK="%~1"
)

if !TARGET_APK!=="" (
    echo.
    echo Please drag and drop your built .apk file into this window, then press ENTER:
    set /p USER_APK=
    set TARGET_APK="!USER_APK:"=!"
)

if not exist !TARGET_APK! (
    echo.
    echo [ERROR] APK file not found at: !TARGET_APK!
    echo Please build your APK from Unity (File ^> Build Settings ^> Build) and run this script again.
    echo.
    pause
    exit /b 1
)

echo.
echo [3/4] Installing VR APK: !TARGET_APK!...
%ADB_EXE% install -r -d -g !TARGET_APK!
if !errorlevel! neq 0 (
    echo.
    echo [ERROR] Installation failed.
    echo If this is an incompatible signature error, please ensure the headset is connected
    echo with USB debugging enabled, and try again.
    echo.
    pause
    exit /b 1
)

echo.
echo [4/4] Launching VR App on Meta Quest headset...
%ADB_EXE% shell am start -n com.DefaultCompany.stationwalkthrough/com.unity3d.player.UnityPlayerActivity >nul 2>&1
if !errorlevel! neq 0 (
    %ADB_EXE% shell monkey -p com.DefaultCompany.stationwalkthrough -c android.intent.category.LAUNCHER 1 >nul 2>&1
)

echo.
echo ===================================================================
echo [SUCCESS] VR App installed and launched on your Meta Quest headset!
echo Put on your headset to experience the walkthrough in Full HD VR.
echo ===================================================================
echo.
pause
