<#
.SYNOPSIS
Runs every test project in tests/ and reports one summary.

.DESCRIPTION
The test projects are Microsoft.Testing.Platform (MTP v2) executables. The .NET 9 SDK's dotnet test
cannot run MTP v2, so while the repository builds with it each built test assembly is run directly.
The local gate, CI and the release workflow all run this script.

Build first; the script never builds. It fails (#437):
- before running anything, when Kaleido.slnx lists no test projects, or the test projects on disk
  differ from the ones it lists (a project that is not in the solution would never run);
- when a project's build output is missing: each project runs exactly the assembly MSBuild
  evaluates as its TargetPath for the configuration, never another target framework's or a
  reference assembly;
- for a project whose run fails: a failed test, a non-zero exit code, no test summary (a crash), or
  no tests run.
The decisions live in RunTests.Functions.ps1; Test-RunTests.ps1 tests them.

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
. (Join-Path $PSScriptRoot 'RunTests.Functions.ps1')

$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$results = [IO.Path]::GetFullPath($ResultsDirectory)

$expected = Get-ExpectedTestProjects (Join-Path $repo 'Kaleido.slnx')
$problems = @(Compare-TestProjects $expected (Get-TestProjectsOnDisk $repo))

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "Test project set: $_" }
    Write-Error 'The test project set is not the one Kaleido.slnx lists; nothing was run.'
}

$total = 0
$failed = 0
$failedProjects = @()

foreach ($relative in $expected) {
    $project = Join-Path $repo $relative
    $name = [IO.Path]::GetFileNameWithoutExtension($relative)
    $assembly = Resolve-TestAssembly $project $Configuration

    if (-not (Test-Path $assembly)) {
        Write-Error "$name was not built in $Configuration ($assembly): build the solution first."
    }

    $arguments = @($assembly, '--results-directory', $results)
    if ($Coverage) {
        $arguments += @('--coverage', '--coverage-output-format', 'cobertura')
    }

    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $summary = Get-RunSummary $output
    $failure = Get-RunFailure $exitCode $summary
    $projectTotal = if ($summary) { $summary.Total } else { 0 }
    $projectFailed = if ($summary) { $summary.Failed } else { 0 }
    $total += $projectTotal
    $failed += $projectFailed

    if (-not $failure) {
        Write-Host ("{0,-48} passed {1,5}" -f $name, $projectTotal)
    }
    else {
        # The full output only for a failing project: the passing ones log a lot (hosts, requests).
        Write-Host ("{0,-48} FAILED {1,5} of {2} ({3})" -f $name, $projectFailed, $projectTotal, $failure)
        $output | Write-Host
        $failedProjects += "$name ($failure)"
    }
}

Write-Host ''
Write-Host "Test run summary: $(if ($failedProjects) { 'Failed!' } else { 'Passed!' })"
Write-Host "  projects: $($expected.Count)"
Write-Host "  total: $total"
Write-Host "  failed: $failed"

if ($failedProjects) {
    $failedProjects | ForEach-Object { Write-Host "  failed project: $_" }
    exit 1
}
