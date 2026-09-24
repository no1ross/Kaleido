namespace Kaleido.Http.Queryable.Contracts;

[ExcludeFromCodeCoverage]
public sealed record PageableContract
{
    public int DefaultSize { get; init; }

    public int MaxSize { get; init; }

    public static PageableContract FromMetadata(
        PageableMetadata metadata)
    {
        return new PageableContract
        {
            DefaultSize = metadata.DefaultSize,
            MaxSize = metadata.MaxSize
        };
    }
}
