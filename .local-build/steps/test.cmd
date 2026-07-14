@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: test.cmd - run the tests in Release configuration.
:: Thin wrapper over: dotnet test -c Release
:: -------------------------------------------------------

cd /d "%~dp0..\.."

echo [INFO] dotnet test -c Release ...
echo.

dotnet test -c Release
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Tests failed. Exit code: %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Tests passed ^(Release^).
exit /b 0
