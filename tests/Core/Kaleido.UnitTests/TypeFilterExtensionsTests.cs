using Kaleido.Exceptions;

namespace Kaleido.UnitTests;

public sealed class TypeFilterExtensionsTests : SutFixture
{
    [Fact]
    public void PassesTypeFilter_WithoutFilter_ReturnsTrue()
    {
        Assert.True(typeof(string).PassesTypeFilter(null, "code", "step"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PassesTypeFilter_ReturnsTheFilterResult(bool expected)
    {
        Assert.Equal(expected, typeof(string).PassesTypeFilter(_ => expected, "code", "step"));
    }

    [Fact]
    public void PassesTypeFilter_WhenTheFilterThrows_WrapsItInAConfigurationException()
    {
        var failure = new InvalidOperationException("filter failed");

        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => typeof(string).PassesTypeFilter(_ => throw failure, "my_code", "query source"));

        Assert.Equal("my_code", exception.Code);
        Assert.Same(failure, exception.InnerException);
        Assert.Contains("query source 'System.String'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PassesTypeFilter_WhenTheFilterIsCanceled_PropagatesTheCancellation()
    {
        Assert.Throws<OperationCanceledException>(
            () => typeof(string).PassesTypeFilter(_ => throw new OperationCanceledException(), "my_code", "step"));
    }

    [Fact]
    public void PassesTypeFilter_ForATypeWithoutFullName_UsesTheTypeName()
    {
        var genericParameter = typeof(List<>).GetGenericArguments()[0];

        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => genericParameter.PassesTypeFilter(_ => throw new InvalidOperationException(), "my_code", "step"));

        Assert.Contains("step 'T'", exception.Message, StringComparison.Ordinal);
    }
}
