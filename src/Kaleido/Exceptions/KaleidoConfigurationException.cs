namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when required Kaleido configuration is missing or invalid.
/// </summary>
public sealed class KaleidoConfigurationException : Exception
{
    public KaleidoConfigurationException(string message)
        : base(message) { }

    public KaleidoConfigurationException(string message, Exception innerException)
        : base(message, innerException) { }
}
