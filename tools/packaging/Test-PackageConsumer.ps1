#Requires -Version 5.1
<#
.SYNOPSIS
    Checks the packed Kaleido packages and builds a clean consumer from them (#319).

.DESCRIPTION
    1. Package contents: every library package carries its assembly, XML docs and README and has a
       symbols package (.snupkg); Kaleido.Analyzers carries only the analyzer; Kaleido depends on it.
    2. A clean consumer (a new project outside the repository, restoring from the local feed only for
       Kaleido packages, into its own package folder) builds a valid step and handler without any
       repository-only diagnostic (KAL0xxx, KAL1xxx, Meziantou MA, Roslynator RCS).
    3. The same consumer with a deliberate violation (a [ProcessStep] with an empty Version, which compiles) fails with the
       consumer rule KAL2001: the analyzer flows to consumers through the package dependency.

.PARAMETER Packages
    The folder holding the packed .nupkg/.snupkg files (CI: artifacts).

.EXAMPLE
    dotnet pack ... -o artifacts; pwsh tools/packaging/Test-PackageConsumer.ps1 -Packages artifacts
#>
param(
    [string] $Packages = "artifacts"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$feed = (Resolve-Path $Packages).Path
$failures = [System.Collections.Generic.List[string]]::new()

function Get-Entries([string] $package) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package)
    try { return @($zip.Entries | ForEach-Object FullName) } finally { $zip.Dispose() }
}

# --- 1. Package contents ---------------------------------------------------------------------
$nupkgs = Get-ChildItem $feed -Filter *.nupkg | Where-Object { $_.Name -notlike "*.symbols.nupkg" }
$kaleido = $nupkgs | Where-Object { $_.Name -match '^Kaleido\.(\d[^\\/]*)\.nupkg$' } | Select-Object -First 1
if (-not $kaleido) { throw "No Kaleido.<version>.nupkg in $feed." }
$version = [regex]::Match($kaleido.Name, '^Kaleido\.(\d.*)\.nupkg$').Groups[1].Value

foreach ($package in $nupkgs) {
    $id = $package.Name.Substring(0, $package.Name.Length - ".$version.nupkg".Length)
    $entries = Get-Entries $package.FullName

    if ($id -eq "Kaleido.Analyzers") {
        if ($entries -notcontains "analyzers/dotnet/cs/Kaleido.Analyzers.dll") { $failures.Add("$id has no analyzers/dotnet/cs/Kaleido.Analyzers.dll") }
        if ($entries | Where-Object { $_ -like "lib/*" }) { $failures.Add("$id ships a lib/ folder; it must be analyzer-only") }
        continue
    }

    foreach ($expected in "lib/net8.0/$id.dll", "lib/net8.0/$id.xml", "README.md") {
        if ($entries -notcontains $expected) { $failures.Add("$id has no $expected") }
    }

    if (-not (Test-Path (Join-Path $feed "$id.$version.snupkg"))) { $failures.Add("$id has no symbols package ($id.$version.snupkg)") }
}

$nuspec = & {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($kaleido.FullName)
    try { $reader = [System.IO.StreamReader]::new($zip.GetEntry("Kaleido.nuspec").Open()); try { $reader.ReadToEnd() } finally { $reader.Dispose() } } finally { $zip.Dispose() }
}
if ($nuspec -notmatch '<dependency id="Kaleido.Analyzers"') { $failures.Add("Kaleido does not depend on Kaleido.Analyzers") }

# --- 2./3. Clean consumer ----------------------------------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) "kaleido-consumer-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

try {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Kaleido" /><package pattern="Kaleido.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $work "nuget.config")

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RestorePackagesPath>packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Kaleido" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $work "Consumer.csproj")

    @"
using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Consumer;

[ProcessStep(DisplayName = "Start", Description = "Starts the case.", Version = "1.0")]
public sealed record StartStep : IProcessStep;

public sealed class StartHandler : IProcessStepHandler<StartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
"@ | Set-Content (Join-Path $work "Valid.cs")

    $valid = & dotnet build $work -nologo 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { $failures.Add("The clean consumer does not build:`n$valid") }

    $leaked = [regex]::Matches($valid, '\b(KAL[01]\d{3}|MA\d{4}|RCS\d{4})\b') | ForEach-Object Value | Sort-Object -Unique
    if ($leaked) { $failures.Add("Repository-only diagnostics reached the consumer: $($leaked -join ', ')") }

    @"
using Kaleido.Processor;

namespace Consumer;

[ProcessStep(DisplayName = "Broken", Description = "Has an empty version.", Version = "")]
public sealed record BrokenStep : IProcessStep;
"@ | Set-Content (Join-Path $work "Violation.cs")

    $broken = & dotnet build $work -nologo 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $broken -notmatch '\bKAL2001\b') { $failures.Add("A [ProcessStep] with an empty Version did not fail with KAL2001:`n$broken") }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "::error::$_" }
    exit 1
}

Write-Host "Packages $version`: contents complete; a clean consumer builds, gets KAL2001, and no repository-only rule."
