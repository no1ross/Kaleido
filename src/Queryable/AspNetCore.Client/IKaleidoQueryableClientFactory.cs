namespace Kaleido.Queryable.AspNetCore.Client;

public interface IKaleidoQueryableClientFactory
{
    IKaleidoQueryableClient GetClient(string name);
}
