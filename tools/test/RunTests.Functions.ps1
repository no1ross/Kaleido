# The decisions of Run-Tests.ps1, dot-sourced by it and by its self-test (Test-RunTests.ps1, #437):
# which test projects must run, which assembly is a project's build output, and what a run's summary
# says. Each function returns data; the caller decides how to fail.

# The test projects the solution lists under tests/ (repository-relative paths, forward slashes).
function Get-ExpectedTestProjects([string] $solution) {
    @([regex]::Matches((Get-Content $solution -Raw), '<Project Path="(tests/[^"]+\.csproj)"') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

# Every test project on disk under tests/, as repository-relative paths like the solution's.
function Get-TestProjectsOnDisk([string] $repository) {
    $root = [IO.Path]::GetFullPath($repository).TrimEnd('\', '/')
    @(Get-ChildItem (Join-Path $root 'tests') -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\', '/') } | Sort-Object -Unique)
}

# What is wrong with the project set: an empty expectation, or projects missing on either side.
function Compare-TestProjects([string[]] $expected, [string[]] $onDisk) {
    $problems = @()

    if (-not $expected -or $expected.Count -eq 0) {
        return @('the solution lists no test projects under tests/')
    }

    $notInSolution = @($onDisk | Where-Object { $expected -notcontains $_ })
    $notOnDisk = @($expected | Where-Object { $onDisk -notcontains $_ })

    if ($notInSolution.Count -gt 0) { $problems += "not in Kaleido.slnx (so never run): $($notInSolution -join ', ')" }
    if ($notOnDisk.Count -gt 0) { $problems += "in Kaleido.slnx but not on disk: $($notOnDisk -join ', ')" }

    $problems
}

# The assembly the project builds for the configuration, as MSBuild evaluates it (TargetPath): never a
# stale output of another target framework, nor a reference assembly.
function Resolve-TestAssembly([string] $project, [string] $configuration) {
    $path = (& dotnet msbuild $project -nologo "-getProperty:TargetPath" "-p:Configuration=$configuration" 2>&1 | Out-String).Trim()

    if ($LASTEXITCODE -ne 0 -or -not $path) {
        throw "Could not evaluate the TargetPath of $project ($configuration): $path"
    }

    $path
}

# The summary a Microsoft.Testing.Platform run prints ('total: N', 'failed: N'), or $null when it
# printed none (a crash, a host that never started). The last occurrence wins.
function Get-RunSummary([object[]] $output) {
    $text = @($output | ForEach-Object { "$_" })
    $total = $text | Select-String '^\s*total:\s*(\d+)\s*$' | Select-Object -Last 1
    $failed = $text | Select-String '^\s*failed:\s*(\d+)\s*$' | Select-Object -Last 1

    if (-not $total -or -not $failed) {
        return $null
    }

    [pscustomobject]@{
        Total = [int]$total.Matches[0].Groups[1].Value
        Failed = [int]$failed.Matches[0].Groups[1].Value
    }
}

# Why a run failed, or $null when it passed: the native exit code, a missing summary, no tests run, or
# failed tests (also when the exit code says otherwise).
function Get-RunFailure([int] $exitCode, $summary) {
    if (-not $summary) { return 'no test summary: the run crashed or printed none' }
    if ($summary.Total -le 0) { return 'ran no tests' }
    if ($summary.Failed -gt 0) { return "$($summary.Failed) test(s) failed" }
    if ($exitCode -ne 0) { return "exit code $exitCode" }
    $null
}
