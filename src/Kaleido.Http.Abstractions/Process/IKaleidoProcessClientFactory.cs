namespace Kaleido.Http.Abstractions.Process;

public interface IKaleidoProcessClientFactory
{
    IKaleidoProcessClient GetClient(string name);
}
