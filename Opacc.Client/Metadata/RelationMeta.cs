namespace Opacc.Client.Metadata;

/// <summary>
/// Resolved metadata for a relation declared via <c>[BoRelation]</c>.
/// </summary>
public record RelationMeta(
    string  Alias,
    string  Source,
    string  Filter,
    string  Count,
    string? OrderArray,
    string? GetBoId,
    string  RawDefinition   // Pre-built "Alias,Source,Count,OrderArray,Filter" for Query Related=
)
{
    // Backward-compat aliases used by existing builder code
    public string TargetBo      => Source;
    public string JoinCondition => Filter;
}
