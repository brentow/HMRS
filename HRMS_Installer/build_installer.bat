@echo off
echo ============================================
echo  HRMS Installer Build Script
echo ============================================
echo.

:: Step 1: Build the HRMS project in Release mode
echo [1/2] Building HRMS in Release mode...
cd /d "%~dp0.."
dotnet build HRMS\HRMS.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo Build successful.
echo.

:: Step 2: Run Inno Setup compiler
echo [2/2] Compiling installer with Inno Setup...
set ISCC_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" (
    echo ERROR: Inno Setup 6 not found!
    echo Please install Inno Setup from https://jrsoftware.org/isdl.php
    pause
    exit /b 1
)

"%ISCC_PATH%" "%~dp0HRMS_Setup.iss"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Inno Setup compilation failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Installer built successfully!
echo  Output: HRMS_Installer\Output\HRMS_Setup_1.0.1.exe
echo ============================================
pause
