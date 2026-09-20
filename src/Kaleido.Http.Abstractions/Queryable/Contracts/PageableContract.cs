namespace Kaleido.Http.Abstractions.Queryable.Contracts;

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
