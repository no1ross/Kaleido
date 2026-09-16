@echo off
setlocal

echo.
echo ==========================================
echo   Cleaning .NET Build Artifacts
echo ==========================================
echo.

for %%D in (
    bin
    obj
    .vs
    TestResults
    artifacts
    coverage
) do (
    echo Removing %%D folders...
    for /f "delims=" %%I in ('dir /s /b /ad %%D 2^>nul') do (
        echo    %%I
        rmdir /s /q "%%I"
    )
)

echo.
echo ==========================================
echo   Restoring Solution
echo ==========================================
echo.

set FOUND_SOLUTION=

for %%S in (*.sln) do (
    set FOUND_SOLUTION=1
    echo Restoring %%S...
    dotnet restore "%%S"
)

if not defined FOUND_SOLUTION (
    echo No solution file found. Running generic restore...
    dotnet restore
)

echo.
echo ==========================================
echo   Cleanup Complete
echo ==========================================
echo.

pause