using Kaleido.Json;
using System.Text.Json;

namespace Kaleido.UnitTests;

public sealed class KaleidoEnumConverterFactoryTests
{
    [Fact]
    public void CanConvert_WhenTypeIsEnum_ReturnsTrue()
    {
        var factory = new KaleidoEnumConverterFactory();

        var result = factory.CanConvert(typeof(TestStatus));

        Assert.True(result);
    }

    [Fact]
    public void CanConvert_WhenTypeIsNotEnum_ReturnsFalse()
    {
        var factory = new KaleidoEnumConverterFactory();

        var result = factory.CanConvert(typeof(string));

        Assert.False(result);
    }

    [Fact]
    public void CreateConverter_ReturnsEnumConverterForRequestedType()
    {
        var factory = new KaleidoEnumConverterFactory();

        var converter =
            factory.CreateConverter(
                typeof(TestStatus),
                new JsonSerializerOptions());

        Assert.IsType<KaleidoEnumConverter<TestStatus>>(
            converter);
    }

    private enum TestStatus
    {
        Active
    }
}
