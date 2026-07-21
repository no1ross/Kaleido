using System;
using System.Collections.Generic;
using System.Text;

namespace Kaleido.Metadata;

/// <summary>
/// Central metadata cache for all registered record record types.
///
/// This service is responsible for converting record definitions
/// (RecordAttribute, FilterableAttribute, SearchableAttribute, etc.)
/// into RuntimeRecordMetadata instances.
///
/// Metadata is generated once and reused for the lifetime of the
/// application to avoid repeated reflection operations.
/// </summary>
/// <remarks>
/// Architecture Role:
///     Metadata Provider
///
/// Used By:
///     - RecordQueryEngine<T>
///     - RecordDescriptorFactory
///     - Registration/Discovery components
///
/// Implementations:
///     - RecordMetadataCatalog
///
/// Replaceability:
///     Consumers should depend on this abstraction only.
///     Alternate implementations may load metadata from
///     external registries or precompiled sources.
/// </remarks>
public interface IRecordMetadataCatalog
{
    RuntimeRecordMetadata GetMetadata<TRecord>() where TRecord : class;
    RuntimeRecordMetadata GetMetadata(Type recordType);
}
