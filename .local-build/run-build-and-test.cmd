@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: run-build-and-test.cmd - the "proven-locally" gate: build
:: then test, both in Release, aborting on the first failure.
:: Run this green before pushing.
:: -------------------------------------------------------

set "STEPS=%~dp0steps\"

echo.
echo === build ===
call "%STEPS%build.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === test ===
call "%STEPS%test.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo [OK] Build - Test succeeded. State is proven locally.
exit /b 0
