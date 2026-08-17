using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Records;

internal interface IQueryContextRegistrationValidator
{
    void Validate(
        IReadOnlyCollection<Type> queryContextTypes,
        IServiceCollection services);
}
