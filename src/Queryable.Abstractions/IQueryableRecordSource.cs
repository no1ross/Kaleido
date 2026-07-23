using System;
using System.Collections.Generic;
using System.Text;

namespace Kaleido.Queryable
{
    /// <summary>
    /// Provides the root queryable source for a record.
    ///
    /// Implementations define where data originates.
    ///
    /// Examples:
    ///     - Entity Framework DbSet<TRecord>
    ///     - In-memory collections
    ///     - LINQ-to-SQL
    ///     - OData adapters
    ///
    /// The framework does not assume a specific persistence
    /// technology.
    ///
    /// This interface represents the boundary between
    /// framework infrastructure and application data.
    /// </summary>
    public interface IQueryableRecordSource<TRecord>
            where TRecord : class
    {
        IQueryable<TRecord> CreateQuery(RecordExecutionContext executionContext);
    }
}