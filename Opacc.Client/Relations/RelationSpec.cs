namespace Opacc.Client.Relations;

/// <summary>
/// Typed descriptor for a relation on an Opacc Business Object.
///
/// Instead of magic strings, use the nested Relations class generated on your model:
///   .Include(Addr.Relations.Country)
///   .Include(SalDoc.Relations.Addr, SalDoc.Relations.Art)
///
/// The implicit string conversion keeps backward-compatibility with
/// APIs that still accept a raw alias string.
/// </summary>
public readonly struct RelationSpec<T>
    where T : class, IOpaccModel, new()
{
    /// <summary>The relation alias used in Opacc query parameters (e.g. "Country").</summary>
    public string Alias { get; }

    /// <summary>The raw relation definition string used in [BoRelation] and Related= parameters.</summary>
    public string RawDefinition { get; }

    public RelationSpec(string alias, string rawDefinition)
    {
        Alias = alias;
        RawDefinition = rawDefinition;
    }

    /// <summary>Allows a RelationSpec to be passed wherever a string alias is expected.</summary>
    public static implicit operator string(RelationSpec<T> spec) => spec.Alias;

    public override string ToString() => Alias;
}
