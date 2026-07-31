using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Exceptions;

public sealed class ValidationException
    : Exception
{
    public ValidationException(
        IReadOnlyCollection<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyCollection<ValidationError> Errors { get; }
}


public sealed record ValidationError(
    string Code,
    string Message);