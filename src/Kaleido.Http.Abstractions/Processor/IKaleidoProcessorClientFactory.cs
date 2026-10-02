namespace Kaleido.Http.Processor;

public interface IKaleidoProcessorClientFactory
{
    IKaleidoProcessorClient GetClient(string name);
}
