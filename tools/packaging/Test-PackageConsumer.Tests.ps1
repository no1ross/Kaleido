#Requires -Version 5.1
<#
.SYNOPSIS
    Self-test of the package gate (#436): Test-PackageConsumer.ps1 passes on the real release set and
    fails, naming the cause, for every broken one.

.DESCRIPTION
    Runs the gate on the packed set (it must pass), then on copies of it, each broken one way, and
    asserts the gate fails with the expected message:
    - each package of the release set removed, in turn;
    - a duplicate package;
    - a package at another version (mixed versions);
    - an unexpected package id;
    - a symbols package without its PDB;
    - a repository-only analyzer asset injected into a library package.
    Each run is a child process, so the gate's exit code is observed as CI observes it. Exits 1 when
    any check fails. CI runs it after the gate (build.yml).

.PARAMETER Packages
    The folder holding the packed .nupkg/.snupkg files (CI: artifacts).

.EXAMPLE
    ./tools/packaging/Test-PackageConsumer.Tests.ps1 -Packages artifacts
#>
param(
    [string] $Packages = "artifacts"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$gate = Join-Path $PSScriptRoot "Test-PackageConsumer.ps1"
$shell = (Get-Process -Id $PID).Path
$feed = (Resolve-Path $Packages).Path
$failures = [System.Collections.Generic.List[string]]::new()
$work = Join-Path ([System.IO.Path]::GetTempPath()) "kaleido-package-gate-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

$expected = "Kaleido", "Kaleido.Analyzers", "Kaleido.Http", "Kaleido.Http.Abstractions", "Kaleido.Http.Client",
    "Kaleido.Http.Mcp", "Kaleido.Observability.OpenTelemetry", "Kaleido.Provider.SQLite"
$version = [regex]::Match((Get-ChildItem $feed -Filter "Kaleido.Http.*.nupkg" | Where-Object Name -match '^Kaleido\.Http\.\d' | Select-Object -First 1).Name, '^Kaleido\.Http\.(.+)\.nupkg$').Groups[1].Value

function Invoke-Gate([string] $packages) {
    $output = & $shell -NoProfile -ExecutionPolicy Bypass -File $gate -Packages $packages 2>&1 | Out-String
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

# A fresh copy of the release set, to break one way.
function New-Feed([string] $name) {
    $copy = Join-Path $work $name
    New-Item -ItemType Directory -Path $copy | Out-Null
    Copy-Item (Join-Path $feed "*") $copy
    $copy
}

function Edit-Package([string] $path, [scriptblock] $edit) {
    $zip = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Update)
    try { & $edit $zip } finally { $zip.Dispose() }
}

function Set-NuspecText($zip, [string] $find, [string] $replace) {
    $entry = $zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName -like '*.nuspec' } | Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($entry.Open()); $text = $reader.ReadToEnd(); $reader.Dispose()
    $name = $entry.FullName
    $entry.Delete()
    $writer = [System.IO.StreamWriter]::new($zip.CreateEntry($name).Open())
    $writer.Write($text.Replace($find, $replace)); $writer.Dispose()
}

function Assert-Fails([string] $case, [string] $packages, [string] $because) {
    $run = Invoke-Gate $packages
    if ($run.ExitCode -ne 0 -and $run.Output.Contains($because)) {
        Write-Host "ok    $case"
    }
    else {
        Write-Host "FAIL  $case (exit code $($run.ExitCode); expected a failure naming '$because')"
        Write-Host $run.Output
        $failures.Add($case)
    }
}

try {
    # The real release set passes.
    $real = Invoke-Gate $feed
    if ($real.ExitCode -eq 0) { Write-Host "ok    the release set passes" } else { Write-Host "FAIL  the release set passes`n$($real.Output)"; $failures.Add("the release set passes") }

    # Each package removed in turn.
    foreach ($id in $expected) {
        $broken = New-Feed "without-$id"
        Remove-Item (Join-Path $broken "$id.$version.nupkg")
        Assert-Fails "without $id" $broken "Package $id is missing from the release set"
    }

    # A duplicate: the same package twice under two file names.
    $broken = New-Feed "duplicate"
    Copy-Item (Join-Path $broken "Kaleido.Http.$version.nupkg") (Join-Path $broken "Kaleido.Http.copy.$version.nupkg")
    Assert-Fails "a duplicate package" $broken "Package Kaleido.Http appears 2 times"

    # Mixed versions: one package repacked at another version.
    $broken = New-Feed "mixed"
    $other = Join-Path $broken "Kaleido.Http.Client.9.9.9.nupkg"
    Move-Item (Join-Path $broken "Kaleido.Http.Client.$version.nupkg") $other
    Edit-Package $other { param($zip) Set-NuspecText $zip "<version>$version</version>" "<version>9.9.9</version>" }
    Assert-Fails "mixed versions" $broken "The packages have more than one version"

    # An unexpected id.
    $broken = New-Feed "unexpected"
    $unexpected = Join-Path $broken "Kaleido.Unexpected.$version.nupkg"
    Copy-Item (Join-Path $broken "Kaleido.Http.$version.nupkg") $unexpected
    Edit-Package $unexpected { param($zip) Set-NuspecText $zip "<id>Kaleido.Http</id>" "<id>Kaleido.Unexpected</id>" }
    Assert-Fails "an unexpected package" $broken "Unexpected package Kaleido.Unexpected"

    # Empty symbols: the PDB removed from a symbols package.
    $broken = New-Feed "empty-symbols"
    Edit-Package (Join-Path $broken "Kaleido.Http.$version.snupkg") { param($zip) @($zip.Entries | Where-Object { $_.FullName -like '*.pdb' }) | ForEach-Object { $_.Delete() } }
    Assert-Fails "a symbols package without its PDB" $broken "has no lib/net8.0/Kaleido.Http.pdb"

    # A repository-only analyzer injected into a library package.
    $broken = New-Feed "leaked-analyzer"
    Edit-Package (Join-Path $broken "Kaleido.Http.$version.nupkg") {
        param($zip)
        $writer = [System.IO.BinaryWriter]::new($zip.CreateEntry("analyzers/dotnet/cs/Kaleido.Analyzers.Source.dll").Open())
        $writer.Write([byte[]](0x4D, 0x5A)); $writer.Dispose()
    }
    Assert-Fails "a repository-only analyzer asset" $broken "Repository-only analyzer asset reaches consumers: Kaleido.Http analyzers/dotnet/cs/Kaleido.Analyzers.Source.dll"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    Write-Host "::error::Package gate self-test: $($failures.Count) check(s) failed: $($failures -join '; ')"
    exit 1
}

Write-Host "Package gate self-test: the release set passes and every broken set fails."
exit 0
