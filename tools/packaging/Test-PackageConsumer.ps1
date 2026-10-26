#Requires -Version 5.1
<#
.SYNOPSIS
    Checks that the packed Kaleido packages are the complete release set, and consumes every one of
    them from clean projects (#319, #436).

.DESCRIPTION
    1. The release set (manifest): exactly the seven library packages and Kaleido.Analyzers, each once,
       all at one version, read from each package's .nuspec (not its file name); one symbols package
       (.snupkg) per library and no other. A missing, duplicate, unexpected or mixed-version package
       fails.
    2. Package contents and metadata: every package carries README, icon, LICENSE and NOTICE and
       declares the license, readme, icon, project and repository (url and commit); every library
       carries its assembly and XML docs; Kaleido.Analyzers carries only the analyzer, and Kaleido
       depends on it; every dependency on a sibling Kaleido package is exactly the packed version
       ([x.y.z], #431).
    3. Symbols: every library symbols package holds its PDB. With -RequireDeterministic (CI builds),
       every PDB maps its source paths to /_/ and carries a Source Link map to the repository (#322).
    Any failure so far stops the script before a consumer is restored.
    4. Analyzer isolation, from what NuGet resolves: the host consumer (below) is restored and its
       project.assets.json read. No Kaleido package but Kaleido.Analyzers contributes an analyzer,
       Kaleido.Analyzers contributes only Kaleido.Analyzers.dll, and no repository-only analyzer
       (Kaleido.Analyzers.Source/.Testing, Meziantou, Roslynator) reaches the consumer.
    5. A clean core consumer (Kaleido only) builds a valid step and handler without any
       repository-only diagnostic (KAL0xxx, KAL1xxx, MA, RCS), and a deliberate violation (a
       [ProcessStep] with an empty Description) fails with KAL2001: the consumer analyzer flows through the
       package dependency.
    6. A clean host consumer of every runtime package (Kaleido.Http, .Http.Client, .Http.Mcp,
       .Provider.SQLite, .Observability.OpenTelemetry) builds, starts, answers its registry and MCP
       endpoints, and stops.
    The consumers live outside the repository and restore Kaleido packages from the local feed only,
    into their own package folder. Test-PackageConsumer.Tests.ps1 proves the gate fails for each
    broken release set.

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

# The release set (#436): every shipped package, by id. Adding a package means adding it here.
$libraries = @(
    "Kaleido",
    "Kaleido.Http",
    "Kaleido.Http.Abstractions",
    "Kaleido.Http.Client",
    "Kaleido.Http.Mcp",
    "Kaleido.Observability.OpenTelemetry",
    "Kaleido.Provider.SQLite"
)
$analyzer = "Kaleido.Analyzers"
$expected = $libraries + $analyzer
$repositoryUrl = "https://github.com/no1ross/Kaleido"

$feed = (Resolve-Path $Packages).Path
$failures = [System.Collections.Generic.List[string]]::new()

function Stop-IfFailed([string] $stage) {
    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Host "::error::$_" }
        Write-Host "Package gate failed at: $stage."
        exit 1
    }
}

# The package's entries and its .nuspec (the one at the package root).
function Read-Package([string] $path) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($zip.Entries | ForEach-Object FullName)
        $nuspecEntry = @($zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.FullName -like '*.nuspec' })
        if ($nuspecEntry.Count -ne 1) { throw "$([IO.Path]::GetFileName($path)) has $($nuspecEntry.Count) root .nuspec files." }
        $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        [pscustomobject]@{
            File = [IO.Path]::GetFileName($path)
            Path = $path
            Id = "$($metadata.id)"
            Version = "$($metadata.version)"
            Metadata = $metadata
            Entries = $entries
        }
    }
    finally { $zip.Dispose() }
}

# --- 1. The release set ----------------------------------------------------------------------------
$packed = @(Get-ChildItem $feed -Filter *.nupkg | Where-Object { $_.Name -notlike "*.symbols.nupkg" } | ForEach-Object { Read-Package $_.FullName })
$symbols = @(Get-ChildItem $feed -Filter *.snupkg | ForEach-Object { Read-Package $_.FullName })

foreach ($group in $packed | Group-Object Id) {
    if ($expected -notcontains $group.Name) { $failures.Add("Unexpected package $($group.Name) ($($group.Group[0].File)): it is not in the release set") }
    if ($group.Count -gt 1) { $failures.Add("Package $($group.Name) appears $($group.Count) times: $(($group.Group | ForEach-Object File) -join ', ')") }
}
foreach ($id in $expected | Where-Object { ($packed | ForEach-Object Id) -notcontains $_ }) {
    $failures.Add("Package $id is missing from the release set")
}

$versions = @($packed + $symbols | ForEach-Object Version | Sort-Object -Unique)
if ($versions.Count -ne 1) {
    $failures.Add("The packages have more than one version ($($versions -join ', ')): the family moves together (#431)")
}
$version = $versions | Select-Object -First 1

foreach ($group in $symbols | Group-Object Id) {
    if ($libraries -notcontains $group.Name) { $failures.Add("Unexpected symbols package $($group.Group[0].File): only the libraries have symbols") }
    if ($group.Count -gt 1) { $failures.Add("Symbols package $($group.Name) appears $($group.Count) times") }
}
foreach ($id in $libraries | Where-Object { ($symbols | ForEach-Object Id) -notcontains $_ }) {
    $failures.Add("Package $id has no symbols package (.snupkg)")
}

Stop-IfFailed "the release set"

# --- 2. Contents and metadata ------------------------------------------------------------------------
foreach ($package in $packed) {
    $id = $package.Id
    $metadata = $package.Metadata

    foreach ($required in "README.md", "LICENSE", "NOTICE", "$($metadata.icon)") {
        if (-not $required -or $package.Entries -notcontains $required) { $failures.Add("$id has no $(if ($required) { $required } else { 'icon' })") }
    }

    if ("$($metadata.license.type)" -ne "expression" -or "$($metadata.license.'#text')" -ne "Apache-2.0") { $failures.Add("$id does not declare the Apache-2.0 license expression") }
    if ("$($metadata.readme)" -ne "README.md") { $failures.Add("$id does not declare README.md as its readme") }
    if ("$($metadata.projectUrl)" -ne $repositoryUrl) { $failures.Add("$id has project url '$($metadata.projectUrl)', not $repositoryUrl") }
    if ("$($metadata.repository.url)" -ne $repositoryUrl -or -not "$($metadata.repository.commit)") { $failures.Add("$id does not declare its repository ($repositoryUrl) and commit") }

    # The packages move together (#431): every dependency on a sibling is exactly this version.
    foreach ($dependency in @($metadata.dependencies.group.dependency) + @($metadata.dependencies.dependency) | Where-Object { $_ -and "$($_.id)" -like "Kaleido*" }) {
        if ("$($dependency.version)" -ne "[$version]") {
            $failures.Add("$id depends on $($dependency.id) $($dependency.version); sibling packages must depend on exactly [$version]")
        }
    }

    if ($id -eq $analyzer) {
        if ($package.Entries -notcontains "analyzers/dotnet/cs/Kaleido.Analyzers.dll") { $failures.Add("$id has no analyzers/dotnet/cs/Kaleido.Analyzers.dll") }
        if ($package.Entries | Where-Object { $_ -like "lib/*" }) { $failures.Add("$id ships a lib/ folder; it must be analyzer-only") }
        continue
    }

    foreach ($required in "lib/net8.0/$id.dll", "lib/net8.0/$id.xml") {
        if ($package.Entries -notcontains $required) { $failures.Add("$id has no $required") }
    }
}

$core = $packed | Where-Object Id -eq "Kaleido"
if (-not (@($core.Metadata.dependencies.group.dependency) | Where-Object { "$($_.id)" -eq $analyzer })) { $failures.Add("Kaleido does not depend on $analyzer") }

# --- 3. Symbols ------------------------------------------------------------------------------------------
foreach ($package in $symbols) {
    if ($package.Entries -notcontains "lib/net8.0/$($package.Id).pdb") { $failures.Add("$($package.File) has no lib/net8.0/$($package.Id).pdb") }
}

if ($RequireDeterministic) {
    if ($PSVersionTable.PSEdition -ne 'Core') { throw "-RequireDeterministic needs PowerShell 7 (pwsh): it reads the PDBs with System.Reflection.Metadata." }

    $sourceLinkKind = [Guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A'

    foreach ($package in $symbols) {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($package.Path)
        try {
            foreach ($entry in $zip.Entries | Where-Object { $_.FullName -like '*.pdb' }) {
                $buffer = [System.IO.MemoryStream]::new()
                $stream = $entry.Open(); $stream.CopyTo($buffer); $stream.Dispose(); $buffer.Position = 0
                $provider = [System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($buffer)
                try {
                    $reader = $provider.GetMetadataReader()
                    $paths = @($reader.Documents | ForEach-Object { $reader.GetString($reader.GetDocument($_).Name) })
                    $local = @($paths | Where-Object { -not $_.StartsWith('/_/') })
                    if ($local.Count -gt 0) { $failures.Add("$($package.File) $($entry.FullName) has build-machine source paths, e.g. $($local[0])") }

                    $sourceLink = @($reader.CustomDebugInformation | ForEach-Object { $reader.GetCustomDebugInformation($_) } |
                        Where-Object { $reader.GetGuid($_.Kind) -eq $sourceLinkKind } |
                        ForEach-Object { [System.Text.Encoding]::UTF8.GetString($reader.GetBlobBytes($_.Value)) })
                    if (-not ($sourceLink | Where-Object { $_ -match 'https://raw\.githubusercontent\.com/' })) { $failures.Add("$($package.File) $($entry.FullName) has no Source Link map to the repository") }
                }
                finally { $provider.Dispose(); $buffer.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
}

Stop-IfFailed "the package contents"

# --- Consumers -------------------------------------------------------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) "kaleido-consumer-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $work | Out-Null

try {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="packages" />
  </config>
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

    # Nothing from a parent folder (a Directory.Build.props above the temp folder) reaches the consumers.
    "<Project />" | Set-Content (Join-Path $work "Directory.Build.props")
    "<Project />" | Set-Content (Join-Path $work "Directory.Build.targets")
    "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>" | Set-Content (Join-Path $work "Directory.Packages.props")

    $hostDir = Join-Path $work "Host"
    $coreDir = Join-Path $work "Core"
    New-Item -ItemType Directory -Path $hostDir, $coreDir | Out-Null

    # --- 6. (project) The host consumer: every runtime package -----------------------------------------
    $runtimeReferences = ($libraries | Where-Object { $_ -notin "Kaleido", "Kaleido.Http.Abstractions" } |
        ForEach-Object { "    <PackageReference Include=""$_"" Version=""$version"" />" }) -join "`n"

    @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
$runtimeReferences
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $hostDir "Host.csproj")

    @"
using System.ComponentModel;
using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Mcp;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Kaleido.Provider.SQLite;

// Every runtime package in one host: HTTP, a Kaleido client, MCP, the SQLite stores, OpenTelemetry.
var database = Path.Combine(Path.GetTempPath(), "kaleido-consumer-" + Guid.NewGuid().ToString("N") + ".db");
var connection = "Data Source=" + database;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Kaleido:Clients:host:BaseUrl"] = "http://127.0.0.1:9/",
    ["Kaleido:Clients:host:RoutePrefix"] = "host",
});

builder.Services
    .AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "host";
        o.Assemblies = [typeof(StartStep).Assembly];
        o.AuthorizationMode = KaleidoAuthorizationMode.None;
    })
    .AddHttp()
    .AddHttpClients()
    .ForwardCallerToken()
    .AddMcp()
    .AddSqliteContextStore(connection)
    .AddSqliteOutboxStore(connection)
    .AddSqliteIdempotencyStore(connection)
    .AddOpenTelemetry();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync();
}

app.MapKaleidoHttp();
app.MapKaleidoMcp("/mcp");

await app.StartAsync();

try
{
    using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

    var registry = await http.GetAsync("/host/registry");
    var body = await registry.Content.ReadAsStringAsync();
    if (!registry.IsSuccessStatusCode || !body.Contains("StartStep", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("registry: " + (int)registry.StatusCode + " " + body);
        return 1;
    }

    var started = await http.PostAsJsonAsync("/host/processes/steps/StartStep", new { processStep = new { } });
    if (!started.IsSuccessStatusCode)
    {
        Console.Error.WriteLine("step: " + (int)started.StatusCode + " " + await started.Content.ReadAsStringAsync());
        return 1;
    }

    // Any answer but 404 means the MCP endpoint is mapped (a GET without a session is refused).
    var mcp = await http.GetAsync("/mcp");
    if (mcp.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.Error.WriteLine("mcp: not mapped");
        return 1;
    }

    Console.WriteLine("consumer host: registry, step and MCP endpoint answered");
    return 0;
}
finally
{
    await app.StopAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    File.Delete(database);
}

[ProcessStep(DisplayName = "Start", Description = "Starts the case.")]
public sealed record StartStep : IProcessStep;

public sealed class StartHandler : IProcessStepHandler<StartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
"@ | Set-Content (Join-Path $hostDir "Program.cs")

    # --- 4. Analyzer isolation, from the restored assets ----------------------------------------------
    $restore = & dotnet restore $hostDir -nologo 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { $failures.Add("The host consumer does not restore:`n$restore") }
    Stop-IfFailed "restoring the host consumer"

    $assets = Get-Content (Join-Path $hostDir "obj/project.assets.json") -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq "package" }) {
        $packageId = $library.Name.Split('/')[0]
        $analyzerFiles = @($library.Value.files | Where-Object { $_ -like "analyzers/*" -and $_ -like "*.dll" })

        foreach ($file in $analyzerFiles) {
            $leaked =
                ($packageId -like "Kaleido*" -and $packageId -ne $analyzer) -or
                ($packageId -eq $analyzer -and $file -ne "analyzers/dotnet/cs/Kaleido.Analyzers.dll") -or
                ($file -match '(Kaleido\.Analyzers\.(Source|Testing)|Meziantou|Roslynator)[^/]*\.dll$')

            if ($leaked) { $failures.Add("Repository-only analyzer asset reaches consumers: $packageId $file") }
        }
    }
    if (-not ($assets.libraries.PSObject.Properties | Where-Object { $_.Name -like "$analyzer/*" })) { $failures.Add("$analyzer does not reach the consumer") }
    Stop-IfFailed "analyzer isolation"

    # --- 5. The core consumer: analyzer diagnostics --------------------------------------------------
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Kaleido" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $coreDir "Core.csproj")

    @"
using Kaleido.Processor;
using Kaleido.Processor.Execution;

namespace Consumer;

[ProcessStep(DisplayName = "Start", Description = "Starts the case.")]
public sealed record StartStep : IProcessStep;

public sealed class StartHandler : IProcessStepHandler<StartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep processStep, ProcessStepContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}
"@ | Set-Content (Join-Path $coreDir "Valid.cs")

    $valid = & dotnet build $coreDir -nologo 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { $failures.Add("The clean core consumer does not build:`n$valid") }

    $leakedDiagnostics = [regex]::Matches($valid, '\b(KAL[01]\d{3}|MA\d{4}|RCS\d{4})\b') | ForEach-Object Value | Sort-Object -Unique
    if ($leakedDiagnostics) { $failures.Add("Repository-only diagnostics reached the consumer: $($leakedDiagnostics -join ', ')") }

    @"
using Kaleido.Processor;

namespace Consumer;

[ProcessStep(DisplayName = "Broken", Description = "")]
public sealed record BrokenStep : IProcessStep;
"@ | Set-Content (Join-Path $coreDir "Violation.cs")

    $broken = & dotnet build $coreDir -nologo 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $broken -notmatch '\bKAL2001\b') { $failures.Add("A [ProcessStep] with an empty Description did not fail with KAL2001:`n$broken") }

    # --- 6. The host consumer: build, start, answer, stop ---------------------------------------------
    $built = & dotnet build $hostDir -nologo --no-restore 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("The host consumer does not build:`n$built")
    }
    else {
        $run = Start-Process dotnet -ArgumentList "run", "--project", "`"$hostDir`"", "--no-build" -NoNewWindow -PassThru `
            -RedirectStandardOutput (Join-Path $work "host.out") -RedirectStandardError (Join-Path $work "host.err")
        # Windows PowerShell reports no ExitCode for a process whose handle was never read.
        $null = $run.Handle
        if (-not $run.WaitForExit(120000)) {
            $run.Kill()
            $failures.Add("The host consumer did not stop within 120 s")
        }
        elseif ($run.ExitCode -ne 0) {
            $failures.Add("The host consumer failed (exit code $($run.ExitCode)):`n$(Get-Content (Join-Path $work 'host.out') -Raw)`n$(Get-Content (Join-Path $work 'host.err') -Raw)")
        }
    }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Stop-IfFailed "the consumers"

Write-Host "Packages $version`: the complete release set; contents, metadata and symbols complete; analyzers isolated; the core consumer builds and gets KAL2001; the host consumer of every runtime package starts and answers."

# The last native command was the violation build, which fails by design; a pwsh step in GitHub
# Actions ends with that $LASTEXITCODE unless the script sets its own.
exit 0
