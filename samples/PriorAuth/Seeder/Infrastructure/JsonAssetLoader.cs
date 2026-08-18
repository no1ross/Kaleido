using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.Seeder.Infrastructure;

internal sealed class JsonAssetLoader
{
    public T Load<T>(
        string relativeAssetPath,
        JsonSerializerOptions? options = null)
    {
        var path =
            Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                relativeAssetPath);

        var json =
            File.ReadAllText(path);

        return JsonSerializer.Deserialize<T>(
                   json,
                   options ?? CreateDefaultJsonOptions())
               ?? throw new InvalidOperationException(
                   $"Failed to deserialize '{relativeAssetPath}'.");
    }

    public JsonSerializerOptions CreateDefaultJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public JsonSerializerOptions CreateEnumJsonOptions()
    {
        var options = CreateDefaultJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
