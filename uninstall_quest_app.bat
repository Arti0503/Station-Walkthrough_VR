@echo off
echo ========================================================
echo Meta Quest App Uninstaller (Fix Incompatible Signature)
echo ========================================================
echo.

set ADB_EXE=""

if exist "C:\Users\%USERNAME%\AppData\Local\Programs\SideQuest\resources\platform-tools\adb.exe" (
    set ADB_EXE="C:\Users\%USERNAME%\AppData\Local\Programs\SideQuest\resources\platform-tools\adb.exe"
) else if exist "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" (
    set ADB_EXE="C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
) else (
    where adb >nul 2>&1
    if %errorlevel% equ 0 (
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

echo [1/2] Checking connected Meta Quest device...
%ADB_EXE% devices
echo.

echo [2/2] Uninstalling conflicting package: com.DefaultCompany.stationwalkthrough...
%ADB_EXE% uninstall com.DefaultCompany.stationwalkthrough
echo.

echo ========================================================
echo If you saw 'Success' above, the conflicting app is removed!
echo You can now install your new APK in SideQuest.
echo ========================================================
echo.
pause
