namespace Opacc.Client.Operations.Pagination;

public sealed class OpaccPage<T>
{
    public List<T> Items { get; }
    public string? NextCursor { get; }
    public bool HasNextPage => NextCursor != null;
    public int Count => Items.Count;

    public OpaccPage(List<T> items, string? nextCursor)
    {
        Items = items;
        NextCursor = nextCursor;
    }
}
