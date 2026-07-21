using Kaleido.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kaleido.Validation;

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
public interface IRecordQueryValidator
{
    void Validate(KaleidoQueryRequest request, RuntimeRecordMetadata metadata);
}
