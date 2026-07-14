@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: pack-local.cmd - convenience chain: clear the local NuGet
:: cache, then pack into the local folder feed. Kept separate
:: from run-build-and-test.cmd (the build/test proven-locally gate).
:: -------------------------------------------------------

set "STEPS=%~dp0steps\"

echo.
echo === clear-local-nuget-cache ===
call "%STEPS%clear-local-nuget-cache.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === pack ===
call "%STEPS%pack.cmd"
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo [OK] Clear-cache - Pack succeeded.
exit /b 0
