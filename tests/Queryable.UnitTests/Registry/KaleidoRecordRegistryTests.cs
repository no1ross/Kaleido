using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Registry;

namespace Kaleido.UnitTests.Registry;

public sealed class KaleidoRecordRegistryTests
{
    private readonly RecordRegistry _sut;

    public KaleidoRecordRegistryTests()
    {
        _sut = new RecordRegistry(
        [
            TestData.Registration(),
            TestData.OtherRegistration()
        ]);
    }

    [Fact]
    public void Registrations_Should_Return_All_Registrations()
    {
        Assert.Equal(
            2,
            _sut.Registrations.Count);
    }

    [Fact]
    public void GetAll_Should_Return_All_Registrations()
    {
        var result = _sut.GetAll();

        Assert.Equal(
            2,
            result.Count);
    }

    [Fact]
    public void Find_By_Name_Should_Return_Registration()
    {
        var result =
            _sut.FindByName("testrecord");

        Assert.NotNull(result);

        Assert.Equal(
            "testrecord",
            result!.RuntimeMetadata.Key);
    }

    [Fact]
    public void Find_By_Name_Should_Be_Case_Insensitive()
    {
        var result =
            _sut.FindByName("TESTRECORD");

        Assert.NotNull(result);

        Assert.Equal(
            "testrecord",
            result!.RuntimeMetadata.Key);
    }

    [Fact]
    public void Find_By_Name_Should_Return_Null_When_Not_Found()
    {
        var result =
            _sut.FindByName("missing");

        Assert.Null(result);
    }

    [Fact]
    public void Find_By_Type_Should_Return_Registration()
    {
        var result =
            _sut.FindByType(typeof(TestRecord));

        Assert.NotNull(result);

        Assert.Equal(
            typeof(TestRecord),
            result!.RecordType);
    }

    [Fact]
    public void Find_By_Type_Should_Return_Null_When_Not_Found()
    {
        var result =
            _sut.FindByType(typeof(MissingRecord));

        Assert.Null(result);
    }

    [Fact]
    public void GetRequired_By_Name_Should_Return_Registration()
    {
        var result =
            _sut.GetRegistration("testrecord");

        Assert.Equal(
            typeof(TestRecord),
            result.RecordType);
    }

    [Fact]
    public void GetRequired_By_Name_Should_Throw_When_Not_Found()
    {
        var exception =
            Assert.Throws<KeyNotFoundException>(
                () => _sut.GetRegistration("missing"));

        Assert.Contains(
            "missing",
            exception.Message);
    }

    [Fact]
    public void GetRequired_By_Type_Should_Return_Registration()
    {
        var result =
            _sut.GetRegistration(typeof(TestRecord));

        Assert.Equal(
            "testrecord",
            result.RuntimeMetadata.Key);
    }

    [Fact]
    public void GetRequired_By_Type_Should_Throw_When_Not_Found()
    {
        var exception =
            Assert.Throws<KeyNotFoundException>(
                () => _sut.GetRegistration(typeof(MissingRecord)));

        Assert.Contains(
            nameof(MissingRecord),
            exception.Message);
    }

    private static class TestData
    {
        public static RecordRegistration Registration()
        {
            return new RecordRegistration(
                typeof(TestRecord),
                Metadata(typeof(TestRecord).Name));
        }

        public static RecordRegistration OtherRegistration()
        {
            return new RecordRegistration(
                typeof(OtherRecord),
                Metadata(typeof(OtherRecord).Name));
        }

        public static RuntimeRecordMetadata Metadata(string name)
        {
            return new RuntimeRecordMetadata(
                Key: name.ToLower(),
                Name: name.ToUpper(),
                Version: "1.0.0",
                Description: null,
                Source: "Unit Test",
                Fields: [],
                AllowedQueries: [],
                Pageable: null);
        }
    }

    private sealed class TestRecord
    {
    }

    private sealed class OtherRecord
    {
    }

    private sealed class MissingRecord
    {
    }
}