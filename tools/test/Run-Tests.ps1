<#
.SYNOPSIS
Runs every test project in tests/ and reports one summary.

.DESCRIPTION
The test projects are Microsoft.Testing.Platform (MTP v2) executables. The .NET 9 SDK's dotnet test
cannot run MTP v2, so while the repository builds with it each built test assembly is run directly.
The local gate, CI and the release workflow all run this script.

Build first; the script never builds. A project whose run fails (a failed test, a crash, or no tests
run) fails the script.

.EXAMPLE
./tools/test/Run-Tests.ps1
./tools/test/Run-Tests.ps1 -Configuration Release -Coverage
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    # Cobertura coverage per project, written to the results directory (CI merges it).
    [switch] $Coverage,

    [string] $ResultsDirectory = (Join-Path $PSScriptRoot '../../TestResults')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$results = [IO.Path]::GetFullPath($ResultsDirectory)
$projects = Get-ChildItem (Join-Path $repo 'tests') -Recurse -Filter '*.csproj' | Sort-Object Name

# The last 'name: N' line of a run's summary, or 0 when the run printed none (it crashed).
function Get-Count([object[]] $output, [string] $name) {
    $line = $output | Select-String "^\s*${name}:\s*(\d+)" | Select-Object -Last 1
    if ($line) { [int]$line.Matches[0].Groups[1].Value } else { 0 }
}

$total = 0
$failed = 0
$failedProjects = @()

foreach ($project in $projects) {
    $name = $project.BaseName
    $assembly = Get-ChildItem (Join-Path $project.DirectoryName "bin/$Configuration") -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if (-not $assembly) {
        Write-Error "$name was not built in $Configuration (bin/$Configuration/**/$name.dll): build the solution first."
    }

    $arguments = @($assembly.FullName, '--results-directory', $results)
    if ($Coverage) {
        $arguments += @('--coverage', '--coverage-output-format', 'cobertura')
    }

    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $projectTotal = Get-Count $output 'total'
    $projectFailed = Get-Count $output 'failed'
    $total += $projectTotal
    $failed += $projectFailed

    if ($exitCode -eq 0) {
        Write-Host ("{0,-48} passed {1,5}" -f $name, $projectTotal)
    }
    else {
        # The full output only for a failing project: the passing ones log a lot (hosts, requests).
        Write-Host ("{0,-48} FAILED {1,5} of {2} (exit code {3})" -f $name, $projectFailed, $projectTotal, $exitCode)
        $output | Write-Host
        $failedProjects += "$name (exit code $exitCode)"
    }
}

Write-Host ''
Write-Host "Test run summary: $(if ($failedProjects) { 'Failed!' } else { 'Passed!' })"
Write-Host "  projects: $($projects.Count)"
Write-Host "  total: $total"
Write-Host "  failed: $failed"

if ($failedProjects) {
    $failedProjects | ForEach-Object { Write-Host "  failed project: $_" }
    exit 1
}
