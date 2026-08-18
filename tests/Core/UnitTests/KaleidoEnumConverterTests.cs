using Kaleido.Json;
using System.Text.Json;

namespace Kaleido.UnitTests;

public sealed class KaleidoEnumConverterTests
{
    [Fact]
    public void Read_WhenValueMatchesEnum_IgnoresCase()
    {
        var converter = new KaleidoEnumConverter<TestStatus>();
        var json = "\"active\""u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();

        var result =
            converter.Read(
                ref reader,
                typeof(TestStatus),
                new JsonSerializerOptions());

        Assert.Equal(
            TestStatus.Active,
            result);
    }

    [Fact]
    public void Read_WhenValueIsInvalid_ThrowsJsonException()
    {
        var converter = new KaleidoEnumConverter<TestStatus>();
        var json = "\"missing\""u8.ToArray();
        var reader = new Utf8JsonReader(json);
        reader.Read();

        JsonException? exception = null;

        try
        {
            converter.Read(
                ref reader,
                typeof(TestStatus),
                new JsonSerializerOptions());
        }
        catch (JsonException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);

        Assert.Contains(
            "Invalid TestStatus value 'missing'.",
            exception.Message);
    }

    [Fact]
    public void Write_WritesEnumName()
    {
        var converter = new KaleidoEnumConverter<TestStatus>();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        converter.Write(
            writer,
            TestStatus.Inactive,
            new JsonSerializerOptions());

        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "\"Inactive\"",
            json);
    }

    private enum TestStatus
    {
        Active,
        Inactive
    }
}
