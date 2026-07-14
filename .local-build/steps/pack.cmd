@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: pack.cmd - pack the solution in Release configuration
:: into the local NuGet folder feed (sibling "local-packages").
:: Thin wrapper over: dotnet pack "<*.slnx>" -c Release --output ...
:: -------------------------------------------------------

set "SLNX_FILE="
for %%F in ("%~dp0..\..\*.slnx") do set "SLNX_FILE=%%F"

if not defined SLNX_FILE (
    echo [ERROR] No .slnx solution found in "%~dp0..\..\".
    exit /b 1
)

echo [INFO] Solution: %SLNX_FILE%
echo [INFO] dotnet pack -c Release --output ..\..\..\local-packages ...
echo.

dotnet pack "%SLNX_FILE%" -c Release --output "%~dp0..\..\..\local-packages"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Pack failed. Exit code: %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Pack succeeded ^(Release^) -^> local-packages.
exit /b 0
