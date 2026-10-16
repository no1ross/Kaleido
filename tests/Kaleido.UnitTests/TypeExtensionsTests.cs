namespace Kaleido.UnitTests;

public sealed class TypeExtensionsTests
    : SutFixture
{
    private interface IHandler<T>;

    private interface IOtherHandler<T>;

    private interface IUnused<T>;

    private sealed class StringHandler : IHandler<string>, IOtherHandler<int>, IDisposable
    {
        public void Dispose()
        {
        }
    }

    [Fact]
    public void ImplementsGenericInterface_MatchesAnyOfTheOpenDefinitions()
    {
        Assert.True(typeof(StringHandler).ImplementsGenericInterface(typeof(IUnused<>), typeof(IHandler<>)));
        Assert.False(typeof(StringHandler).ImplementsGenericInterface(typeof(IUnused<>)));
        Assert.False(typeof(object).ImplementsGenericInterface(typeof(IHandler<>)));
    }

    [Fact]
    public void ImplementsGenericInterfaceFor_RequiresTheFirstArgumentToMatch()
    {
        Assert.True(typeof(StringHandler).ImplementsGenericInterfaceFor(typeof(string), typeof(IHandler<>)));
        Assert.False(typeof(StringHandler).ImplementsGenericInterfaceFor(typeof(int), typeof(IHandler<>)));
    }

    [Fact]
    public void GetGenericInterfacesFor_ReturnsTheMatchingClosedInterfaces()
    {
        var result = typeof(StringHandler).GetGenericInterfacesFor(typeof(int), typeof(IHandler<>), typeof(IOtherHandler<>));

        Assert.Equal([typeof(IOtherHandler<int>)], result);
    }

    [Fact]
    public void GetGenericInterfaces_ReturnsEveryClosedInterfaceOfTheDefinitions()
    {
        var result = typeof(StringHandler).GetGenericInterfaces(typeof(IHandler<>), typeof(IOtherHandler<>));

        Assert.Equal(2, result.Length);
        Assert.Contains(typeof(IHandler<string>), result);
        Assert.Contains(typeof(IOtherHandler<int>), result);
        Assert.Empty(typeof(StringHandler).GetGenericInterfaces(typeof(IUnused<>)));
    }
}
