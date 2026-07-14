@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: build.cmd - build the solution in Release configuration.
:: Thin wrapper over: dotnet build "<*.slnx>" -c Release
:: -------------------------------------------------------

set "SLNX_FILE="
for %%F in ("%~dp0..\..\*.slnx") do set "SLNX_FILE=%%F"

if not defined SLNX_FILE (
    echo [ERROR] No .slnx solution found in "%~dp0..\..\".
    exit /b 1
)

echo [INFO] Solution: %SLNX_FILE%
echo [INFO] dotnet build -c Release ...
echo.

dotnet build "%SLNX_FILE%" -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed. Exit code: %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Build succeeded ^(Release^).
exit /b 0
