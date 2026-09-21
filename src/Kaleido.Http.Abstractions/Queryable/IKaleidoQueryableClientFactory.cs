namespace Kaleido.Http.Queryable;

public interface IKaleidoQueryableClientFactory
{
    IKaleidoQueryableClient GetClient(string name);
}
