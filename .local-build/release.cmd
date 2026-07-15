@echo off
chcp 1252 >nul
setlocal enabledelayedexpansion

:: -------------------------------------------------------
:: release.cmd - deliberate, human-run release marking.
:: NOT part of the proven-locally gate (run-build-and-test.cmd);
:: run it only after a green gate, with the version and CHANGELOG
:: already bumped and committed.
::
:: Reads <Version> from the main project .csproj, verifies a matching
:: "## [<version>]" section in CHANGELOG.md, creates and pushes an
:: annotated tag v<version>, then opens a GitHub release via `gh`
:: with notes drawn from that changelog section. Per ADR-0023.
:: -------------------------------------------------------

set "ROOT=%~dp0.."
set "NOTES=%TEMP%\ckl-release-notes-%RANDOM%.md"
set "VERSION="

echo.
echo === resolve version and notes ===
for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0steps\resolve-release.ps1" -RepoRoot "%ROOT%" -NotesOut "%NOTES%"`) do set "VERSION=%%V"
if not defined VERSION (
    echo [ERROR] Could not resolve a release version/notes. See messages above.
    goto :fail
)
echo [INFO] Version: %VERSION%

echo.
echo === git tag ===
git -C "%ROOT%" rev-parse -q --verify "refs/tags/v%VERSION%" >nul
if %ERRORLEVEL% equ 0 ( echo [ERROR] Tag v%VERSION% already exists. & goto :fail )
git -C "%ROOT%" tag -a "v%VERSION%" -m "Release v%VERSION%"
if %ERRORLEVEL% neq 0 ( echo [ERROR] Failed to create tag v%VERSION%. & goto :fail )
git -C "%ROOT%" push origin "v%VERSION%"
if %ERRORLEVEL% neq 0 ( echo [ERROR] Failed to push tag v%VERSION%. & goto :fail )

echo.
echo === github release ===
pushd "%ROOT%"
gh release create "v%VERSION%" --title "v%VERSION%" --notes-file "%NOTES%"
set "GHRC=%ERRORLEVEL%"
popd
if not "%GHRC%"=="0" ( echo [ERROR] gh release create failed. & goto :fail )

del "%NOTES%" >nul 2>&1
echo.
echo [OK] Released v%VERSION% ^(tag pushed, GitHub release created^).
exit /b 0

:fail
del "%NOTES%" >nul 2>&1
exit /b 1
