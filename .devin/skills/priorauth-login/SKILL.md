---
name: priorauth-login
description: Log the priorauth MCP server in as a PriorAuth dev persona (alice, bob, carol). Use when asked to "login to priorauth as <name>", or when the priorauth MCP server lists no tools (expired token).
---

# Log in to the priorauth MCP server

The `priorauth` MCP server is the PriorAuth router's `/mcp` endpoint
(`http://localhost:8080/mcp`). Its `Authorization` header is read from
`.git/priorauth.token` at the repo root. A connected server with **no tools**
means that token is missing or expired.

1. Run (default persona is `bob`):
   ```powershell
   ./samples/PriorAuth/scripts/mcp-login.ps1 <alice|bob|carol>
   ```
   If it fails to connect, the router is not running; start the full compose
   stack under `samples/PriorAuth/Compose/` (`podman compose ...`).
2. Call `mcp_list_tools` for `priorauth` to confirm the tools are listed.

Personas: `alice` (no domain role), `bob` (`radiology`), `carol` (`admin`, `radiology`).
