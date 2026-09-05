namespace Kaleido.Process.AspNetCore.Client;

public interface IKaleidoProcessClientFactory
{
    IKaleidoProcessClient GetClient(string name);
}
