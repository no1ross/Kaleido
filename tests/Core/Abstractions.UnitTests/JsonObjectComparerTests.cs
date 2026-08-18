using Kaleido.Json;

namespace Kaleido.Abstractions.UnitTests;

public sealed class JsonObjectComparerTests
{
    [Fact]
    public void AreEqual_WhenReferencesMatch_ReturnsTrue()
    {
        var value = new TestObject { Id = 1, Name = "Alice" };

        var result = JsonObjectComparer.AreEqual(value, value);

        Assert.True(result);
    }

    [Fact]
    public void AreEqual_WhenEitherValueIsNull_ReturnsFalse()
    {
        var result = JsonObjectComparer.AreEqual(new TestObject(), null);

        Assert.False(result);
    }

    [Fact]
    public void AreEqual_WhenTypesDiffer_ReturnsFalse()
    {
        var result = JsonObjectComparer.AreEqual(new TestObject(), new OtherObject());

        Assert.False(result);
    }

    [Fact]
    public void AreEqual_WhenSerializedJsonMatches_ReturnsTrue()
    {
        var previous = new TestObject { Id = 1, Name = "Alice" };
        var current = new TestObject { Id = 1, Name = "Alice" };

        var result = JsonObjectComparer.AreEqual(previous, current);

        Assert.True(result);
    }

    [Fact]
    public void AreEqual_WhenSerializedJsonDiffers_ReturnsFalse()
    {
        var previous = new TestObject { Id = 1, Name = "Alice" };
        var current = new TestObject { Id = 2, Name = "Alice" };

        var result = JsonObjectComparer.AreEqual(previous, current);

        Assert.False(result);
    }

    private sealed class TestObject
    {
        public int Id { get; init; }

        public string? Name { get; init; }
    }

    private sealed class OtherObject
    {
    }
}
