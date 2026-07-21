namespace Kaleido.UnitTests.Registry;

public sealed class KaleidoRecordRegistryTests
{
    private readonly KaleidoRecordRegistry _sut;

    public KaleidoRecordRegistryTests()
    {
        _sut = new KaleidoRecordRegistry(
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
            _sut.Find("test-record");

        Assert.NotNull(result);

        Assert.Equal(
            "test-record",
            result!.Name);
    }

    [Fact]
    public void Find_By_Name_Should_Be_Case_Insensitive()
    {
        var result =
            _sut.Find("TEST-RECORD");

        Assert.NotNull(result);

        Assert.Equal(
            "test-record",
            result!.Name);
    }

    [Fact]
    public void Find_By_Name_Should_Return_Null_When_Not_Found()
    {
        var result =
            _sut.Find("missing");

        Assert.Null(result);
    }

    [Fact]
    public void Find_By_Type_Should_Return_Registration()
    {
        var result =
            _sut.Find(typeof(TestRecord));

        Assert.NotNull(result);

        Assert.Equal(
            typeof(TestRecord),
            result!.RecordType);
    }

    [Fact]
    public void Find_By_Type_Should_Return_Null_When_Not_Found()
    {
        var result =
            _sut.Find(typeof(MissingRecord));

        Assert.Null(result);
    }

    [Fact]
    public void GetRequired_By_Name_Should_Return_Registration()
    {
        var result =
            _sut.GetRegistration("test-record");

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
            "test-record",
            result.Name);
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
                "test-record",
                typeof(TestRecord),
                Metadata());
        }

        public static RecordRegistration OtherRegistration()
        {
            return new RecordRegistration(
                "other-record",
                typeof(OtherRecord),
                Metadata());
        }

        public static RuntimeRecordMetadata Metadata()
        {
            return new RuntimeRecordMetadata(
                Name: "test-record",
                Version: "1.0.0",
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