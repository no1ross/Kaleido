using Kaleido.Exceptions;

namespace Kaleido.Abstractions.UnitTests;

public sealed class ValidationExceptionTests
{
    [Fact]
    public void Constructor_StoresErrorsAndUsesExpectedMessage()
    {
        IReadOnlyCollection<ValidationError> errors =
        [
            new("code-1", "message-1")
        ];

        var exception = new ValidationException(errors);

        Assert.Equal("One or more validation errors occurred.", exception.Message);
        Assert.Same(errors, exception.Errors);
    }
}
