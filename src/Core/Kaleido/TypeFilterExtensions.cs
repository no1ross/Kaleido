namespace Kaleido;

internal static class TypeFilterExtensions
{
    /// <summary>
    /// Evaluates the configured <see cref="KaleidoServiceOptions.TypeFilter"/> for a candidate type,
    /// converting a filter failure into a <see cref="KaleidoConfigurationException"/>.
    /// </summary>
    internal static bool PassesTypeFilter(
        this Type type,
        Func<Type, bool>? typeFilter,
        string errorCode,
        string typeLabel)
    {
        try
        {
            return typeFilter?.Invoke(type) ?? true;
        }
        // Any failure of the consumer's filter is a configuration error, reported with the type.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new KaleidoConfigurationException(
                errorCode,
                $"The configured TypeFilter failed while evaluating {typeLabel} '{type.FullName ?? type.Name}'.",
                exception);
        }
    }
}
