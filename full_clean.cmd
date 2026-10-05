@echo off
setlocal

rem Usage: full_clean.cmd [--no-pause]

echo.
echo ==========================================
echo   Cleaning .NET Build Artifacts
echo ==========================================
echo.

rem node_modules is skipped: npm packages ship their own bin folders
rem (e.g. @angular/cli/bin) and deleting them corrupts the install.
for %%D in (
    bin
    obj
    .vs
    TestResults
    artifacts
    coverage
) do (
    echo Removing %%D folders...
    for /f "delims=" %%I in ('dir /s /b /ad %%D 2^>nul ^| findstr /v /i /l "node_modules"') do (
        echo    %%I
        rmdir /s /q "%%I"
    )
)

echo.
echo ==========================================
echo   Restoring Solution
echo ==========================================
echo.

dotnet restore "%~dp0Kaleido.slnx"

echo.
echo ==========================================
echo   Restoring npm Packages
echo ==========================================
echo.

for /f "delims=" %%L in ('dir /s /b "%~dp0package-lock.json" 2^>nul ^| findstr /v /i /l "node_modules"') do (
    echo npm ci in %%~dpL
    pushd "%%~dpL"
    call npm ci
    popd
)

echo.
echo ==========================================
echo   Cleanup Complete
echo ==========================================
echo.

if /i not "%~1"=="--no-pause" pause
