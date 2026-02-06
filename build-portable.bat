@echo off
echo ============================================
echo Building NetworkConfigApp Portable Executable
echo ============================================
echo.

cd /d "%~dp0"

echo Restoring packages...
dotnet restore

echo.
echo Building Release...
dotnet build -c Release

echo.
echo ============================================
echo Build complete!
echo.
echo Portable executable location:
echo   src\NetworkConfigApp\bin\Release\net48\NetworkConfigApp.Portable.exe
echo.
echo This single file contains all dependencies and can be
echo copied to any Windows machine with .NET Framework 4.8.
echo ============================================

pause
