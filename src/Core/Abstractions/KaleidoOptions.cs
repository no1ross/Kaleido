namespace Kaleido;

/// <summary>
/// Base class for all Kaleido configuration option POCOs.
/// Subsystems extend this with their own transport-specific properties.
/// All subclasses bind from the <c>"Kaleido"</c> configuration section.
/// </summary>
public abstract class KaleidoOptions
{
    /// <summary>
    /// The configuration section name shared by all Kaleido subsystem options.
    /// </summary>
    public const string SectionName = "Kaleido";
}
