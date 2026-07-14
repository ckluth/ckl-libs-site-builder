@echo off
chcp 1252 >nul
setlocal

:: -------------------------------------------------------
:: clear-local-nuget-cache.cmd - purge this package from the
:: global NuGet cache, so a same-version re-pack is actually
:: picked up by consumers of the local folder feed.
:: -------------------------------------------------------

set "NUGET_BASE=%USERPROFILE%\.nuget\packages"
set "PACKAGE=ckl.libs.sitebuilder"

if exist "%NUGET_BASE%\%PACKAGE%" (
    echo [INFO] Removing: %NUGET_BASE%\%PACKAGE%
    rd /s /q "%NUGET_BASE%\%PACKAGE%"
    if %ERRORLEVEL% neq 0 (
        echo [ERROR] Failed to remove cache entry.
        exit /b %ERRORLEVEL%
    )
    echo [OK] Cache entry removed.
) else (
    echo [INFO] Not found, nothing to do: %NUGET_BASE%\%PACKAGE%
)

exit /b 0
