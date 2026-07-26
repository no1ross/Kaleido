using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Samples.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Kaleido.FunctionalTests.Hosting;

[ApiController]
[Route("v1/records")]
public sealed class KaleidoRecordController
    : ControllerBase
{
    private readonly IQueryableCatalog _catalog;

    public KaleidoRecordController(
        IQueryableCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<RecordMetadata>> GetRecords()
    {
        return Ok(
            _catalog.GetRecordDescriptors());
    }

    [HttpGet("{recordKey}")]
    public ActionResult<RecordMetadata> GetRecord(
        string recordKey)
    {
        var metadata =
            _catalog
                .GetRecordDescriptors()
                .SingleOrDefault(x =>
                    string.Equals(
                        x.Name,
                        recordKey,
                        StringComparison.OrdinalIgnoreCase));

        return metadata is null
            ? NotFound()
            : Ok(metadata);
    }

    [HttpPost("{recordKey}/query")]
    public async Task<ActionResult<QueryResponse<SampleKaleidoRecord>>> Query(
        string recordKey,
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response =
                await _catalog.QueryAsync<SampleKaleidoRecord>(
                    recordKey,
                    request,
                    cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                new
                {
                    error = new
                    {
                        code = "KALEIDO_QUERY_INVALID",
                        message = ex.Message
                    }
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    error = new
                    {
                        code = "KALEIDO_QUERY_INVALID",
                        message = ex.Message
                    }
                });
        }
    }
}