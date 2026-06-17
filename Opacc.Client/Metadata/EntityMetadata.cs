namespace Opacc.Client.Metadata;

public class EntityMetadata
{
    public required string BoEntity { get; init; }
    public required int DefaultIndex { get; init; }

    /// <summary>
    /// Anzahl Segmente des Default-Index — entspricht IdProperties.Count.
    /// Wird für GetBo-Requests als FixedSegsOfBoIndex-Default verwendet.
    /// </summary>
    public int DefaultSegment => IdProperties.Count;

    public required IReadOnlyDictionary<string, PropertyMeta> Properties { get; init; }

    /// <summary>
    /// Properties des Default-Index, geordnet nach SegmentNo (aufsteigend).
    /// </summary>
    public required IReadOnlyList<PropertyMeta> IdProperties { get; init; }

    /// <summary>
    /// Alle Index-Properties, gruppiert nach IndexNo, jeweils nach SegmentNo sortiert.
    /// </summary>
    public required IReadOnlyDictionary<int, IReadOnlyList<PropertyMeta>> AllIndexProperties { get; init; }

    public required IReadOnlyList<PropertyMeta> DefaultProperties { get; init; }
    public required IReadOnlyList<RelationMeta> Relations { get; init; }

    /// <summary>
    /// Gibt die geordneten Key-Properties für einen bestimmten Index zurück.
    /// Gibt eine leere Liste zurück wenn der Index unbekannt ist.
    /// </summary>
    public IReadOnlyList<PropertyMeta> GetIndexProperties(int indexNo)
        => AllIndexProperties.TryGetValue(indexNo, out var props) ? props : [];

    /// <summary>
    /// Baut den Select-String für einen Opacc-Request.
    /// </summary>
    public string BuildSelectString(IEnumerable<string> clrPropertyNames)
    {
        var expressions = new List<string>();

        foreach (var name in clrPropertyNames)
        {
            if (!Properties.TryGetValue(name, out var meta))
                continue;
            if (meta.IsVirtual)
                continue;

            var expr = meta.OoExpression;
            if (!expr.Contains('.'))
                expr = BoEntity + "." + expr;

            expressions.Add(expr);
        }

        return string.Join(", ", expressions);
    }

    /// <summary>
    /// Gibt die Virtual-Attribute-Strings zurück für die angegebenen Properties.
    /// </summary>
    public List<string> GetVirtualAttributes(IEnumerable<string> clrPropertyNames)
    {
        var result = new List<string>();

        foreach (var name in clrPropertyNames)
        {
            if (!Properties.TryGetValue(name, out var meta))
                continue;
            if (!meta.IsVirtual || string.IsNullOrWhiteSpace(meta.VirtualExpression))
                continue;

            result.Add(meta.ClrName + "=" + meta.VirtualExpression);
        }

        return result;
    }

    /// <summary>
    /// Ermittelt welche Relationen für die angegebenen Properties benötigt werden.
    /// </summary>
    public List<RelationMeta> GetRequiredRelations(IEnumerable<string> clrPropertyNames)
    {
        var usedAliases = new HashSet<string>();

        foreach (var name in clrPropertyNames)
        {
            if (Properties.TryGetValue(name, out var meta) && meta.RelationAlias != null)
                usedAliases.Add(meta.RelationAlias);
        }

        return Relations.Where(r => usedAliases.Contains(r.Alias)).ToList();
    }

    private List<string>? _selectableNamesCache;
    private List<string>? _selectableNamesWithQueryOnlyCache;

    /// <summary>
    /// Gibt alle Property-Namen zurück die nicht ignoriert sind.
    /// </summary>
    public List<string> GetAllSelectablePropertyNames(bool includeQueryOnly = false)
    {
        if (includeQueryOnly)
            // Query path: include QueryOnly properties, but exclude those not available in Query
            return _selectableNamesWithQueryOnlyCache ??= Properties.Values
                .Where(p => !p.IsNotAvailableInQuery)
                .Select(p => p.ClrName)
                .ToList();

        // GetBo path: exclude QueryOnly properties, keep NotAvailableInQuery ones (they work fine in GetBo)
        return _selectableNamesCache ??= Properties.Values
            .Where(p => !p.IsQueryOnly)
            .Select(p => p.ClrName)
            .ToList();
    }
}
