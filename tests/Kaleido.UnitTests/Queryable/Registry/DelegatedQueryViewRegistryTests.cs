using System.Reflection;
using Kaleido.Queryable.Registry;

namespace Kaleido.Queryable.UnitTests.Records;

public sealed class DelegatedQueryViewRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static DelegatedQueryViewRegistry CreateSut(
        ITypeDescriber? TypeDescriber = null,
        IConstraintMapper? constraintMapper = null,
        IEnumerable<Type>? queryViewTypes = null)
    {
        var dtm = TypeDescriber ?? CreateDefaultTypeDescriber();
        var cm = constraintMapper ?? CreateDefaultConstraintMapper();

        return new DelegatedQueryViewRegistry(dtm, cm, queryViewTypes ?? []);
    }

    private static ITypeDescriber CreateDefaultTypeDescriber()
    {
        var mock = new Mock<ITypeDescriber>();
        mock.Setup(m => m.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(new DataTypeDescriptor("string"));
        return mock.Object;
    }

    private static IConstraintMapper CreateDefaultConstraintMapper()
    {
        var mock = new Mock<IConstraintMapper>();
        mock.Setup(m => m.Map(It.IsAny<PropertyInfo>()))
            .Returns([]);
        return mock.Object;
    }

    [Fact]
    public void Registrations_WhenEmpty_ReturnsEmptyCollection()
    {
        var sut = CreateSut();
        Assert.Empty(sut.Registrations);
    }

    [Fact]
    public void Find_ByName_WhenNotRegistered_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.Find("nonexistent"));
    }

    [Fact]
    public void Find_ByType_WhenNotRegistered_ReturnsNull()
    {
        var sut = CreateSut();
        Assert.Null(sut.Find(typeof(object)));
    }
}
