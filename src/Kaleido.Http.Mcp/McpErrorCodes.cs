namespace Kaleido.Http.Mcp;

/// <summary>
/// Error codes owned by the Kaleido MCP adapter (<c>mcp_*</c>). Startup codes surface through
/// <see cref="KaleidoConfigurationException"/>; tool-call codes are returned to the agent in the
/// tool result (never as HTTP errors), so the model can explain or recover.
/// </summary>
public static class McpErrorCodes
{
    /// <summary><c>Kaleido:Mcp:BaseUrl</c> is missing or not an absolute http(s) URL.</summary>
    public const string MissingBaseUrl = "mcp_missing_base_url";

    /// <summary><c>Kaleido:Mcp:RegistryPath</c> is missing.</summary>
    public const string MissingRegistryPath = "mcp_missing_registry_path";

    /// <summary><c>Kaleido:Mcp:InstructionsFile</c> points at a file that does not exist.</summary>
    public const string MissingInstructionsFile = "mcp_missing_instructions_file";

    /// <summary>The agent called a tool that is not (or no longer) offered to this caller.</summary>
    public const string UnknownTool = "mcp_unknown_tool";

    /// <summary>The tool arguments are missing a required value or have the wrong shape.</summary>
    public const string InvalidArguments = "mcp_invalid_arguments";

    /// <summary>The Kaleido service returned a non-success HTTP status.</summary>
    public const string ServiceError = "mcp_service_error";

    /// <summary>An answer was submitted but the process has no pending information request.</summary>
    public const string NoPendingInformationRequest = "mcp_no_pending_information_request";
}
