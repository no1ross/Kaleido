using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class StepProcessingMessageTests
    : SutFixture
{
    private static StepProcessingMessage CreateSut(MessageType type) =>
        type switch
        {
            MessageType.Information => StepProcessingMessage.Information("note", "A note."),
            MessageType.Warning => StepProcessingMessage.Warning("note", "A note."),
            _ => StepProcessingMessage.Error("note", "A note."),
        };

    [Theory]
    [InlineData(MessageType.Information)]
    [InlineData(MessageType.Warning)]
    [InlineData(MessageType.Error)]
    public void EachFactory_SetsItsTypeWithTheCodeAndMessage(MessageType type)
    {
        var message = CreateSut(type);

        Assert.Equal(type, message.Type);
        Assert.Equal("note", message.Code);
        Assert.Equal("A note.", message.Message);
    }
}
