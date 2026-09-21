namespace Kaleido.Http.Process;

public interface IKaleidoProcessClientFactory
{
    IKaleidoProcessClient GetClient(string name);
}
