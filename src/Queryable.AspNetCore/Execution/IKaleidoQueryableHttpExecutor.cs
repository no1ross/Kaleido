using Kaleido.Queryable;

namespace Kaleido.Queryable.AspNetCore;

internal interface IKaleidoQueryableHttpExecutor
{
    Task<object> ExecuteAsync(
        string key,
        KaleidoQueryRequest request,
        CancellationToken cancellationToken = default);
}
