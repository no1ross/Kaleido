using Kaleido.Queryable;
using Microsoft.AspNetCore.Mvc;

namespace Kaleido.FunctionalTests.Hosting;

[ApiController]
[Route("v1/records")]
public sealed class KaleidoRecordController : ControllerBase
{
    private readonly IQueryableCatalog _catalog;

    public KaleidoRecordController(IQueryableCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public ActionResult<IEnumerable<RecordDescriptor>> GetRecords()
    {
        return Ok(_catalog.GetAll());
    }

    [HttpGet("{recordKey}")]
    public ActionResult<RecordDescriptor> GetRecord(string recordKey)
    {
        var metadata = _catalog.Get(recordKey);
        return metadata is null ? NotFound() : Ok(metadata);
    }

    //[HttpPost("{recordKey}/query")]
    //public async Task<ActionResult<KaleidoQueryResponse>> Query(
    //    string recordKey,
    //    [FromBody] KaleidoQueryRequest request)
    //{
    //    try
    //    {
    //        var response = await _catalog.QueryAsync(recordKey, request);
    //        return Ok(response);
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        return BadRequest(new { error = new { code = "KALEIDO_QUERY_INVALID", message = ex.Message } });
    //    }
    //    catch (ArgumentException ex)
    //    {
    //        return BadRequest(new { error = new { code = "KALEIDO_QUERY_INVALID", message = ex.Message } });
    //    }
    //}
}
