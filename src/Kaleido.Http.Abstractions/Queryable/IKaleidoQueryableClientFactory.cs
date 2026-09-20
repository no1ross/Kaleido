namespace Kaleido.Http.Abstractions.Queryable;

public interface IKaleidoQueryableClientFactory
{
    IKaleidoQueryableClient GetClient(string name);
}
