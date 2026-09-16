namespace Kaleido.Process.Http.Client;

public interface IKaleidoProcessClientFactory
{
    IKaleidoProcessClient GetClient(string name);
}
