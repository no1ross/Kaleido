namespace Kaleido.Queryable.Client.UnitTests;

public sealed class IKaleidoQueryableClientTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void QueryMethods_TakeOnlyNeutralQueryableRequests()
    {
        var requests = typeof(IKaleidoQueryableClient).GetMethods()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.Name == "request")
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(3, requests.Length);
        Assert.Contains(typeof(QueryRequest), requests);
        Assert.Contains(requests, type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(QueryRequest<>));
        Assert.All(requests, type => Assert.Equal(typeof(QueryRequest).Namespace, type.Namespace));
    }
}
