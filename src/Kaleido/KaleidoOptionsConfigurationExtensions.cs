using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Kaleido;

/// <summary>
/// Lays a configuration section over the options a host passed in code (ADR 0030): configuration
/// overrides code, setting by setting, and the host's instance is never changed.
/// </summary>
/// <remarks>
/// <para>
/// The configuration binder alone does not do that: binding into an existing object writes into its
/// nested objects in place, and appends configured list items to the code's list. Here:
/// </para>
/// <list type="bullet">
/// <item><description>the options record is copied first, and so is every nested options record that
/// configuration touches;</description></item>
/// <item><description>a setting whose key is in the section replaces the code value; a setting whose
/// key is absent keeps it;</description></item>
/// <item><description>a configured collection replaces the code's collection (string-keyed dictionaries
/// with case-insensitive keys, as configuration keys are);</description></item>
/// <item><description>a value that cannot be read as its setting's type is a
/// <see cref="KaleidoConfigurationException"/> with the caller's code and the setting's path.</description></item>
/// </list>
/// <para>Nothing is trimmed, clamped or substituted: validation is the caller's, after the overlay.</para>
/// </remarks>
internal static class KaleidoOptionsConfigurationExtensions
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>A copy of <paramref name="options"/> with every setting <paramref name="section"/> names replaced.</summary>
    /// <typeparam name="T">An options record.</typeparam>
    /// <param name="section">The configuration section that overrides them.</param>
    /// <param name="options">The options from code (or the defaults); not changed.</param>
    /// <param name="errorCode">The area's code for a configured value that cannot be read.</param>
    /// <returns>The options to validate and register.</returns>
    /// <exception cref="KaleidoConfigurationException">A configured value cannot be read as its setting's type.</exception>
    public static T BindOver<T>(this IConfigurationSection section, T options, string errorCode)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return (T)Overlay(options, section, errorCode);
    }

    private static object Overlay(object options, IConfigurationSection section, string errorCode)
    {
        var type = options.GetType();
        var copy = CopyConstructor(type)?.Invoke([options])
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"The options type '{type.Name}' is not a record: configuration cannot be laid over a copy of it.");

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var setting = section.GetSection(property.Name);

            if (property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0 && setting.Exists())
            {
                property.SetValue(copy, Read(property.PropertyType, property.GetValue(copy), setting, errorCode));
            }
        }

        return copy;
    }

    private static object? Read(Type type, object? current, IConfigurationSection setting, string errorCode)
    {
        try
        {
            if (IsOptionsRecord(type) && (current ?? CreateDefault(type)) is { } nested)
            {
                return Overlay(nested, setting, errorCode);
            }

            if (StringDictionaryValueType(type) is { } valueType)
            {
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                var dictionary = Activator.CreateInstance(dictionaryType, StringComparer.OrdinalIgnoreCase) as IDictionary
                    ?? throw new KaleidoFrameworkException(
                        FrameworkErrorCodes.ReflectionError,
                        $"A '{dictionaryType.Name}' could not be created for {setting.Path}.");

                // Each entry is read like any other setting, so a value it cannot read is reported.
                foreach (var entry in setting.GetChildren())
                {
                    dictionary[entry.Key] = Read(valueType, current: null, entry, errorCode);
                }

                return dictionary;
            }

            return setting.Get(type);
        }
        catch (InvalidOperationException exception)
        {
            throw new KaleidoConfigurationException(
                errorCode,
                $"The configured value of {setting.Path} cannot be read as {Describe(type)}.",
                exception);
        }
    }

    // A record other than string or a collection: its compiler-generated copy constructor is how it is
    // copied before configuration is laid over it.
    private static bool IsOptionsRecord(Type type) =>
        type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type) && CopyConstructor(type) is not null;

    private static ConstructorInfo? CopyConstructor(Type type) =>
        type.GetConstructor(AnyInstance, [type]);

    private static object? CreateDefault(Type type) =>
        type.GetConstructor(Type.EmptyTypes) is { } constructor ? constructor.Invoke([]) : null;

    private static Type? StringDictionaryValueType(Type type) =>
        type.IsGenericType
        && type.GetGenericArguments() is [var key, var value]
        && key == typeof(string)
        && type.GetGenericTypeDefinition() is var definition
        && (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(Dictionary<,>))
            ? value
            : null;

    private static string Describe(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type).Name;
}
