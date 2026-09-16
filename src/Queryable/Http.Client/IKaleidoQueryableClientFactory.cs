namespace Kaleido.Queryable.Http.Client;

public interface IKaleidoQueryableClientFactory
{
    IKaleidoQueryableClient GetClient(string name);
}
