//using System.Reflection;
//using Kaleido.Queryable;

//namespace Kaleido.Queryable.AspNetCore;

//internal sealed class KaleidoQueryableHttpExecutor : IKaleidoQueryableHttpExecutor
//{
//    private readonly IKaleidoQueryableRegistry _registry;
//    private readonly IKaleidoQueryable _queryable;

//    public KaleidoQueryableHttpExecutor(
//        IKaleidoQueryableRegistry registry,
//        IKaleidoQueryable queryable)
//    {
//        _registry = registry;
//        _queryable = queryable;
//    }

//    public async Task<object> ExecuteAsync(
//        string key,
//        KaleidoQueryRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        ArgumentException.ThrowIfNullOrWhiteSpace(key);
//        ArgumentNullException.ThrowIfNull(request);

//        request.Query?.Search?.Validate();
//        request.Query?.Filter?.Validate();

//        var registration = _registry.GetRegistration(key)
//            ?? throw new KaleidoQueryableRegistrationNotFoundException(key);

//        var method = typeof(KaleidoQueryableHttpExecutor)
//            .GetMethod(nameof(ExecuteCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
//            .MakeGenericMethod(registration.RecordType);

//        var task = (Task<object>)method.Invoke(
//            this,
//            new object[] { key, request, cancellationToken })!;

//        return await task.ConfigureAwait(false);
//    }

//    private async Task<object> ExecuteCoreAsync<TRecord>(
//        string key,
//        KaleidoQueryRequest request,
//        CancellationToken cancellationToken)
//        where TRecord : class
//    {
//        // This assumes IKaleidoQueryable exposes a strongly typed method similar to:
//        // Task<KaleidoQueryResponse<TRecord>> QueryAsync<TRecord>(string key, KaleidoQueryRequest request, CancellationToken cancellationToken)
//        // If your method name differs, this is the only line that should need adjustment.
//        KaleidoQueryResponse<TRecord> response = await _queryable
//            .QueryAsync<TRecord>(key, request, cancellationToken)
//            .ConfigureAwait(false);

//        return response;
//    }
//}
