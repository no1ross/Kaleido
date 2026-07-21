using Kaleido.Metadata;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class RecordMetadataCatalogTests
{
    private readonly RecordMetadataCatalog _sut = new();

    [Fact]
    public void Different_Record_Types_Should_Return_Different_Instances()
    {
        var first = _sut.GetMetadata<TestRecord>();
        var second = _sut.GetMetadata<AnotherTestRecord>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetMetadata_Should_Return_Same_Instance_For_Same_Type()
    {
        // Act
        var first = _sut.GetMetadata<TestRecord>();
        var second = _sut.GetMetadata<TestRecord>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void Generic_And_NonGeneric_Overloads_Should_Return_Same_Instance()
    {
        // Act
        var generic = _sut.GetMetadata<TestRecord>();
        var nonGeneric = _sut.GetMetadata(typeof(TestRecord));

        // Assert
        Assert.Same(generic, nonGeneric);
    }

    [Fact]
    public void Different_Record_Types_Should_Return_Different_Metadata()
    {
        // Act
        var first = _sut.GetMetadata<TestRecord>();
        var second = _sut.GetMetadata<AnotherTestRecord>();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetMetadata_Should_Throw_For_Invalid_Record()
    {
        Assert.Throws<InvalidOperationException>(
            () => _sut.GetMetadata<InvalidRecord>());
    }
}