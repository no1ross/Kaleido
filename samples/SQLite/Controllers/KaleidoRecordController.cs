using Kaleido.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Kaleido.Samples.SQLite.Controllers
{
    [ApiController]
    [Route("v1/records")]
    public class KaleidoRecordController : ControllerBase
    {
        private readonly IKaleidoCatalog _catalog;

        public KaleidoRecordController(IKaleidoCatalog catalog
            )
        {
            _catalog = catalog;
        }
        [HttpGet("")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<IEnumerable<RecordMetadata>> GetValueSets()
        {
            var metas = _catalog.GetAll();
            return Ok(metas);
        }


        [HttpGet("{recordKey}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<RecordMetadata> GetValueSetMetadata(string recordKey)
        {
            var meta = _catalog.Get(recordKey);
            if (meta == null) return NotFound();
            return Ok(meta);
        }

        [HttpPost("{recordKey}/query")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<KaleidoQueryResponse> QueryValueSet(string recordKey, [FromBody] KaleidoQueryRequest request)
        {
            try
            {
                var response = _catalog.QueryAsync(recordKey, request);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                var error = new
                {
                    error = new
                    {
                        code = "FILTER_NOT_SUPPORTED",
                        message = ex.Message,
                        details = new[] { new { field = "filters", reason = "One or more filters are not supported for this record." } },
                        allowedFilters = new[] { "active", "clientId", "groupName", "clientName", "includeAllClientsOption" }
                    }
                };
                return BadRequest(error);
            }
        }
    }
}
