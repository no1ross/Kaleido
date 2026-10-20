namespace Kaleido.Http.Mcp;

/// <summary>
/// Error codes owned by the Kaleido MCP adapter (<c>mcp_*</c>). Startup codes surface through
/// <see cref="KaleidoConfigurationException"/>. At request time every answer is a well-formed
/// result: tool-call problems come back as a tool result with <c>isError: true</c> and one of these
/// codes (never as an HTTP or protocol error), so the agent can explain or recover.
/// </summary>
public static class McpErrorCodes
{
    /// <summary><c>AddMcp()</c> needs <c>AddHttp()</c> (the registry) on the same Kaleido builder.</summary>
    public const string MissingHttp = "mcp_missing_http";

    /// <summary><c>AddMcp()</c> needs Kaleido clients (<c>Kaleido:Clients</c> + <c>AddHttpClients()</c>) to call services.</summary>
    public const string MissingClients = "mcp_missing_clients";

    /// <summary>
    /// <c>AddMcp()</c> needs to know how tool calls are authorized for the agent's user: register
    /// <c>ForwardCallerToken()</c> or <c>AddCallCredentials&lt;TProvider&gt;()</c> before it (ADR 0022).
    /// </summary>
    public const string MissingCallCredentials = "mcp_call_credentials_not_configured";

    /// <summary><c>Kaleido:Mcp:InstructionsFile</c> points at a file that does not exist.</summary>
    public const string MissingInstructionsFile = "mcp_missing_instructions_file";

    /// <summary>
    /// A <c>Kaleido:Mcp</c> setting (bound or set in the <c>AddMcp</c> configure delegate) is out of
    /// range, e.g. a negative <c>MaxElicitationRounds</c>. Reported at startup, never clamped.
    /// </summary>
    public const string InvalidOptions = "mcp_invalid_options";

    /// <summary>The agent called a tool that is not (or no longer) offered to this caller.</summary>
    public const string UnknownTool = "mcp_unknown_tool";

    /// <summary>The tool arguments are missing a required value or have the wrong shape.</summary>
    public const string InvalidArguments = "mcp_invalid_arguments";

    /// <summary>The Kaleido service refused or failed the call (its status and errors are included).</summary>
    public const string ServiceError = "mcp_service_error";

    /// <summary>
    /// The process was changed by another call (409): nothing was saved. The agent reads the process
    /// state again and decides with the user instead of repeating the call.
    /// </summary>
    public const string ProcessConflict = "mcp_process_conflict";

    /// <summary>The Kaleido service could not be reached, or no Kaleido client is configured for it.</summary>
    public const string ServiceUnavailable = "mcp_service_unavailable";

    /// <summary>The adapter failed unexpectedly while running a tool; the tool did not complete.</summary>
    public const string InternalError = "mcp_internal_error";

    /// <summary>An answer was submitted but the process has no pending information request.</summary>
    public const string NoPendingInformationRequest = "mcp_no_pending_information_request";
}
