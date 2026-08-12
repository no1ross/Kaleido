using Kaleido.Queryable.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Validates incoming QueryRequest instances against
/// record metadata.
///
/// Validation occurs before query compilation and execution.
///
/// Responsibilities:
///   - Field existence validation
///   - Operator support validation
///   - Search mode validation
///   - Sort validation
///   - Named query parameter validation
///   - Paging validation
///
/// This component must not execute queries or perform
/// provider-specific logic.
/// </summary>
internal interface IQueryContextValidator
{
    void Validate(IQueryRequest request, QueryContextRegistration registration, QueryViewRegistration viewRegistration);
}
