using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryExecutionContextTests
    : SutFixture
{
    private sealed record WardParameters(string Ward);

    private sealed record OtherParameters(int Age);

    private static QueryExecutionContext CreateSut(object? viewParameters)
    {
        var request = new Mock<IQueryRequest>();
        request.Setup(x => x.ViewParameters).Returns(viewParameters);
        return new QueryExecutionContext(request.Object);
    }

    [Fact]
    public void TryGetViewParameters_OfTheRequestsType_ReturnsThem()
    {
        var parameters = new WardParameters("B");

        Assert.Same(parameters, CreateSut(parameters).TryGetViewParameters<WardParameters>());
    }

    [Fact]
    public void TryGetViewParameters_WhenTheRequestHasNone_ReturnsNull()
    {
        Assert.Null(CreateSut(null).TryGetViewParameters<WardParameters>());
    }

    [Fact]
    public void TryGetViewParameters_OfAnotherType_IsAValidationError()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() => CreateSut(new OtherParameters(3)).TryGetViewParameters<WardParameters>());

        Assert.Equal(QueryableErrorCodes.InvalidParameterValue, exception.Code);
        Assert.Contains(nameof(OtherParameters), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(WardParameters), exception.Message, StringComparison.Ordinal);
    }
}
