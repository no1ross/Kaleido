<#
.SYNOPSIS
  Logs the priorauth MCP server in as a dev persona (alice, bob, carol).

.DESCRIPTION
  Gets a dev token from the router (POST /auth/login) and writes
  "Bearer <token>" to .git/priorauth.token, which the "priorauth" MCP server
  config reads as its Authorization header:

    "priorauth": {
      "url": "http://localhost:8080/mcp",
      "transport": "http",
      "headers": { "Authorization": "${file:C:/Repos/Kaleido/.git/priorauth.token}" }
    }

  The MCP client picks up the new token on its next connection. Dev tokens
  expire; when the priorauth server shows no tools, run this again.

.EXAMPLE
  ./samples/PriorAuth/scripts/mcp-login.ps1 bob
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('alice', 'bob', 'carol')]
    [string] $Name = 'bob',

    [string] $RouterUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$tokenFile = Join-Path $repoRoot '.git/priorauth.token'

try {
    $response = Invoke-RestMethod -Method Post -Uri "$RouterUrl/auth/login" `
        -ContentType 'application/json' -Body (@{ name = $Name } | ConvertTo-Json)
}
catch {
    throw "Login failed at $RouterUrl/auth/login (is the router running?): $($_.Exception.Message)"
}

Set-Content -NoNewline -Path $tokenFile -Value "Bearer $($response.token)"
Write-Output "priorauth MCP logged in as '$Name' (token written to $tokenFile)."
