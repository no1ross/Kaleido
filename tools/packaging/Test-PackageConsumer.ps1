#Requires -Version 5.1
<#
.SYNOPSIS
    Checks the packed Kaleido packages and builds a clean consumer from them (#319).

.DESCRIPTION
    1. Package contents: every library package carries its assembly, XML docs and README and has a
       symbols package (.snupkg); Kaleido.Analyzers carries only the analyzer; Kaleido depends on it;
       every dependency on a sibling Kaleido package is exactly the packed version ([x.y.z], #431).
    2. A clean consumer (a new project outside the repository, restoring from the local feed only for
       Kaleido packages, into its own package folder) builds a valid step and handler without any
       repository-only diagnostic (KAL0xxx, KAL1xxx, Meziantou MA, Roslynator RCS).
    3. The same consumer with a deliberate violation (a [ProcessStep] with an empty Version, which compiles) fails with the
       consumer rule KAL2001: the analyzer flows to consumers through the package dependency.

    4. With -RequireDeterministic (CI builds): every symbols package's PDBs map their source paths to
       /_/ (no build-machine paths) and carry a Source Link map to the repository (#322).

.PARAMETER Packages
    The folder holding the packed .nupkg/.snupkg files (CI: artifacts).

.PARAMETER RequireDeterministic
    Check deterministic source paths and Source Link in the symbols packages (needs PowerShell 7:
    System.Reflection.Metadata). Packages built outside CI keep real paths, so leave it off locally
    unless you pack with GITHUB_ACTIONS=true.

.EXAMPLE
    dotnet pack ... -o artifacts; pwsh tools/packaging/Test-PackageConsumer.ps1 -Packages artifacts
#>
param(
    [string] $Packages = "artifacts",
    [switch] $RequireDeterministic
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$feed = (Resolve-Path $Packages).Path
$failures = [System.Collections.Generic.List[string]]::new()

function Get-Entries([string] $package) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package)
    try { return @($zip.Entries | ForEach-Object FullName) } finally { $zip.Dispose() }
}

function Get-Nuspec([string] $package, [string] $id) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package)
    try { $reader = [System.IO.StreamReader]::new($zip.GetEntry("$id.nuspec").Open()); try { $reader.ReadToEnd() } finally { $reader.Dispose() } } finally { $zip.Dispose() }
}

# --- 1. Package contents ---------------------------------------------------------------------
$nupkgs = Get-ChildItem $feed -Filter *.nupkg | Where-Object { $_.Name -notlike "*.symbols.nupkg" }
$kaleido = $nupkgs | Where-Object { $_.Name -match '^Kaleido\.(\d[^\\/]*)\.nupkg$' } | Select-Object -First 1
if (-not $kaleido) { throw "No Kaleido.<version>.nupkg in $feed." }
$version = [regex]::Match($kaleido.Name, '^Kaleido\.(\d.*)\.nupkg$').Groups[1].Value

foreach ($package in $nupkgs) {
    $id = $package.Name.Substring(0, $package.Name.Length - ".$version.nupkg".Length)
    $entries = Get-Entries $package.FullName

    # The packages move together (#431): every dependency on a sibling is exactly this version.
    foreach ($dependency in [regex]::Matches((Get-Nuspec $package.FullName $id), '<dependency id="(Kaleido[^"]*)" version="([^"]*)"')) {
        if ($dependency.Groups[2].Value -ne "[$version]") {
            $failures.Add("$id depends on $($dependency.Groups[1].Value) $($dependency.Groups[2].Value); sibling packages must depend on exactly [$version]")
        }
    }

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

$nuspec = Get-Nuspec $kaleido.FullName "Kaleido"
if ($nuspec -notmatch '<dependency id="Kaleido.Analyzers"') { $failures.Add("Kaleido does not depend on Kaleido.Analyzers") }

# --- 4. Deterministic paths and Source Link (CI builds) --------------------------------------------
if ($RequireDeterministic) {
    if ($PSVersionTable.PSEdition -ne 'Core') { throw "-RequireDeterministic needs PowerShell 7 (pwsh): it reads the PDBs with System.Reflection.Metadata." }

    $sourceLinkKind = [Guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A'

    foreach ($symbols in Get-ChildItem $feed -Filter *.snupkg) {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($symbols.FullName)
        try {
            foreach ($entry in $zip.Entries | Where-Object { $_.FullName -like '*.pdb' }) {
                $buffer = [System.IO.MemoryStream]::new()
                $stream = $entry.Open(); $stream.CopyTo($buffer); $stream.Dispose(); $buffer.Position = 0
                $provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($buffer)
                try {
                    $reader = $provider.GetMetadataReader()
                    $paths = @($reader.Documents | ForEach-Object { $reader.GetString($reader.GetDocument($_).Name) })
                    $local = @($paths | Where-Object { -not $_.StartsWith('/_/') })
                    if ($local.Count -gt 0) { $failures.Add("$($symbols.Name) $($entry.FullName) has build-machine source paths, e.g. $($local[0])") }

                    $sourceLink = @($reader.CustomDebugInformation | ForEach-Object { $reader.GetCustomDebugInformation($_) } |
                        Where-Object { $reader.GetGuid($_.Kind) -eq $sourceLinkKind } |
                        ForEach-Object { [System.Text.Encoding]::UTF8.GetString($reader.GetBlobBytes($_.Value)) })
                    if (-not ($sourceLink | Where-Object { $_ -match 'https://raw\.githubusercontent\.com/' })) { $failures.Add("$($symbols.Name) $($entry.FullName) has no Source Link map to the repository") }
                }
                finally { $provider.Dispose(); $buffer.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
}

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

# The last native command was the violation build, which fails by design; a pwsh step in GitHub
# Actions ends with that $LASTEXITCODE unless the script sets its own.
exit 0
