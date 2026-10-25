<#
.SYNOPSIS
Self-test of Run-Tests.ps1's decisions (#437): every way the test gate could pass wrongly fails.

.DESCRIPTION
Dot-sources RunTests.Functions.ps1 and checks, with fixtures in a temporary folder:
- run summaries: a valid one passes; a crash (no summary), zero tests, failed tests (also with exit
  code 0) and a non-zero exit code fail;
- the project set: an empty solution list, a project not in the solution and one not on disk fail;
- the build output: with a stale output of another target framework and a reference assembly next to
  the real one, the resolved assembly is the project's TargetPath.
Exits 1 when any check fails. CI runs it before the tests (build.yml).

.EXAMPLE
./tools/test/Test-RunTests.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'RunTests.Functions.ps1')

$failures = [System.Collections.Generic.List[string]]::new()

function Assert([bool] $condition, [string] $what) {
    if ($condition) { Write-Host "ok    $what" } else { Write-Host "FAIL  $what"; $failures.Add($what) }
}

# --- Run summaries -----------------------------------------------------------------------------
$valid = @('Test run summary: Passed!', '  total: 12', '  failed: 0', '  succeeded: 12', '  skipped: 0')
$summary = Get-RunSummary $valid
Assert ($summary.Total -eq 12 -and $summary.Failed -eq 0) 'a valid summary is parsed'
Assert ($null -eq (Get-RunFailure 0 $summary)) 'a valid run passes'

$crash = @('Unhandled exception. System.IO.FileNotFoundException: Could not load file or assembly', '   at Program.Main()')
Assert ($null -eq (Get-RunSummary $crash)) 'a crash has no summary'
Assert ((Get-RunFailure 0 (Get-RunSummary $crash)) -like 'no test summary*') 'a run without a summary fails, even with exit code 0'

$zero = @('Test run summary: Zero tests ran', '  total: 0', '  failed: 0')
Assert ((Get-RunFailure 0 (Get-RunSummary $zero)) -eq 'ran no tests') 'a run with zero tests fails, even with exit code 0'

$failing = @('  total: 5', '  failed: 2')
Assert ((Get-RunFailure 0 (Get-RunSummary $failing)) -eq '2 test(s) failed') 'failed tests fail the run, even with exit code 0'
Assert ((Get-RunFailure 3 (Get-RunSummary $valid)) -eq 'exit code 3') 'a non-zero exit code fails the run'

$esc = [char]27
$colored = @("Test run summary: $esc[32mPassed!$esc[m", "  total: 12$esc[m", "$esc[0m  failed: 0`r")
Assert ((Get-RunSummary $colored).Total -eq 12) 'a summary with ANSI colors and line endings is parsed'

$repeated = @('  total: 1', '  failed: 0', '  total: 7', '  failed: 1')
Assert ((Get-RunSummary $repeated).Total -eq 7) 'the last summary wins'

# --- Project sets ------------------------------------------------------------------------------
$work = Join-Path ([IO.Path]::GetTempPath()) "kaleido-runtests-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

try {
    $listed = 'tests/A.Tests/A.Tests.csproj', 'tests/B.Tests/B.Tests.csproj'

    Set-Content (Join-Path $work 'empty.slnx') '<Solution><Project Path="src/Lib/Lib.csproj" /></Solution>'
    $none = Get-ExpectedTestProjects (Join-Path $work 'empty.slnx')
    Assert ((@(Compare-TestProjects $none $listed) -join ';') -like '*lists no test projects*') 'an empty solution list fails'

    Set-Content (Join-Path $work 'two.slnx') ('<Solution>' + (($listed | ForEach-Object { "<Project Path=""$_"" />" }) -join '') + '</Solution>')
    $expected = Get-ExpectedTestProjects (Join-Path $work 'two.slnx')
    Assert ($expected.Count -eq 2) 'the solution''s test projects are read'
    Assert (@(Compare-TestProjects $expected $listed).Count -eq 0) 'the same set passes'
    Assert ((@(Compare-TestProjects $expected ($listed + 'tests/C.Tests/C.Tests.csproj')) -join ';') -like '*not in Kaleido.slnx*C.Tests*') 'a project on disk but not in the solution fails'
    Assert ((@(Compare-TestProjects $expected @($listed[0])) -join ';') -like '*not on disk*B.Tests*') 'a project in the solution but not on disk fails'

    foreach ($project in $listed + 'tests/C.Tests/C.Tests.csproj') {
        $path = Join-Path $work $project
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        Set-Content $path '<Project />'
    }
    New-Item -ItemType Directory -Path (Join-Path $work 'tests/A.Tests/obj') -Force | Out-Null
    Set-Content (Join-Path $work 'tests/A.Tests/obj/Ignored.csproj') '<Project />'
    $disk = Get-TestProjectsOnDisk $work
    Assert (($disk -join ';') -eq 'tests/A.Tests/A.Tests.csproj;tests/B.Tests/B.Tests.csproj;tests/C.Tests/C.Tests.csproj') 'projects on disk are found (bin/obj ignored)'

    # --- The build output ----------------------------------------------------------------------
    # A real project built for net8.0, with a stale net7.0 output and a reference assembly beside it.
    $probe = Join-Path $work 'probe/Probe.Tests'
    New-Item -ItemType Directory -Path $probe -Force | Out-Null
    Set-Content (Join-Path $probe 'Probe.Tests.csproj') @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ProduceReferenceAssembly>true</ProduceReferenceAssembly>
  </PropertyGroup>
</Project>
'@
    Set-Content (Join-Path $probe 'Program.cs') 'System.Console.WriteLine("probe");'
    Set-Content (Join-Path $work 'probe/Directory.Build.props') '<Project />'
    Set-Content (Join-Path $work 'probe/Directory.Build.targets') '<Project />'
    Set-Content (Join-Path $work 'probe/Directory.Packages.props') '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>'

    & dotnet build (Join-Path $probe 'Probe.Tests.csproj') -c Release -nologo -v quiet 2>&1 | Out-Null
    Assert ($LASTEXITCODE -eq 0) 'the probe project builds'

    foreach ($decoy in 'bin/Release/net7.0', 'bin/Release/net8.0/ref') {
        New-Item -ItemType Directory -Path (Join-Path $probe $decoy) -Force | Out-Null
        Set-Content (Join-Path $probe "$decoy/Probe.Tests.dll") 'not the build output'
    }

    # The old rule (the first Probe.Tests.dll found recursively) picks a decoy: the fixture reproduces it.
    $firstMatch = Get-ChildItem (Join-Path $probe 'bin/Release') -Recurse -Filter 'Probe.Tests.dll' | Select-Object -First 1
    $expectedPath = [IO.Path]::GetFullPath((Join-Path $probe 'bin/Release/net8.0/Probe.Tests.dll'))
    Assert ($firstMatch.FullName -ne $expectedPath) "the first recursive match is a decoy ($($firstMatch.FullName))"

    $resolved = Resolve-TestAssembly (Join-Path $probe 'Probe.Tests.csproj') 'Release'
    Assert ($resolved -notmatch "`n") 'the TargetPath is one path'
    Assert ([IO.Path]::GetFullPath($resolved) -eq $expectedPath) "the TargetPath is resolved, not a stale or reference assembly ($resolved)"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    Write-Host "::error::Run-Tests self-test: $($failures.Count) check(s) failed: $($failures -join '; ')"
    exit 1
}

Write-Host 'Run-Tests self-test: all checks passed.'
exit 0
