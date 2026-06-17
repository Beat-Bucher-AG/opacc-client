using System.Reflection;
using Opacc.Client.Metadata;

namespace Opacc.Client.Attributes;

// ============================================================
// Non-generic — raw string, for hand-written or scaffold output
// ============================================================

/// <summary>
/// Declares a relation from this BO to another BO/View using a raw Query
/// <c>Related=</c> definition string.
///
/// Prefer the typed overload <see cref="BoRelationAttribute{TRelated}"/> when the target
/// model type is available.
///
/// Format: <c>"Alias,Source,Count,OrderArray,FilterExpression"</c>
/// Example: <c>"Cust,Cust,ToOne,,Cust.Number = SalDoc.CustNo"</c>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BoRelationAttribute : Attribute
{
    public string Alias        { get; }
    public string Source       { get; }
    public string Filter       { get; }
    public string Count        { get; }
    public string? OrderArray  { get; }
    public string? GetBoId     { get; }

    /// <summary>Pre-built Query <c>Related=</c> string.</summary>
    public string RawDefinition { get; }

    // Backward-compat aliases
    public string TargetBo      => Source;
    public string JoinCondition => Filter;

    /// <param name="definition">
    /// Full Query Related= definition: <c>Alias,Source,Count,OrderArray,Filter</c>
    /// </param>
    public BoRelationAttribute(string definition)
    {
        RawDefinition = definition;

        var parts = definition.Split(',');
        Alias  = parts.Length > 0 ? parts[0].Trim() : "";
        Source = parts.Length > 1 ? parts[1].Trim() : Alias;
        Count  = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : "One";
        OrderArray = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null;
        Filter = parts.Length > 4 ? string.Join(",", parts[4..]).Trim() : "";
    }

    public RelationMeta ToRelationMeta() =>
        new(Alias, Source, Filter, Count, OrderArray, GetBoId, RawDefinition);
}

// ============================================================
// Generic — typed, compiler-verified via typeof/nameof
// ============================================================

/// <summary>
/// Declares a typed relation from this BO to <typeparamref name="TRelated"/>.
///
/// The join condition and Query <c>Related=</c> string are assembled at cache-build
/// time from the typed property references — the compiler validates that the
/// referenced properties exist on the correct types.
///
/// <code>
/// // Simple FK (alias = BO name of TRelated):
/// [BoRelation&lt;Cust&gt;(nameof(Cust.Number), nameof(SalDoc.CustNo),
///     Count = RelationCount.ToOne, GetBoId = nameof(SalDoc.CustBoId))]
///
/// // Same BO used twice — explicit alias:
/// [BoRelation&lt;Addr&gt;("DeliveryAddr", nameof(Addr.Number), nameof(SalDoc.DeliveryAddrNo),
///     Count = RelationCount.ToOne, GetBoId = nameof(SalDoc.DeliveryAddrBoId))]
///
/// // Multi-condition join:
/// [BoRelation&lt;Contact&gt;(nameof(Contact.AddrNo), nameof(Addr.Number),
///     AdditionalFilter = "and Contact.IsMainContact = 1")]
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BoRelationAttribute<TRelated> : Attribute
    where TRelated : class, IOpaccModel, new()
{
    // ---------------------------------------------------------------
    // Stored raw (resolved at EntityMetadataCache build time)
    // ---------------------------------------------------------------

    /// <summary>
    /// Custom alias for this relation.
    /// If <see langword="null"/>, the BO entity name of <typeparamref name="TRelated"/> is used.
    /// Required when the same BO is used more than once on a model.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// C# property name on <typeparamref name="TRelated"/> used in the join condition.
    /// Use <c>nameof(TRelated.PropertyName)</c>.
    /// Example: <c>nameof(Cust.Number)</c>
    /// </summary>
    public string RelatedProperty { get; }

    /// <summary>
    /// C# property name on the <em>main</em> BO used in the join condition.
    /// Use <c>nameof(ThisModel.PropertyName)</c>.
    /// Example: <c>nameof(SalDoc.CustNo)</c>
    /// </summary>
    public string MainProperty { get; }

    /// <summary>
    /// Number of rows to return. Defaults to <see cref="RelationCount.One"/>.
    /// </summary>
    public RelationCount Count { get; set; } = RelationCount.One;

    /// <summary>
    /// Optional sort order (Query only). Format: <c>[+Alias.Field,-Alias.Field]</c>
    /// Note: cannot be combined with <see cref="RelationCount.All"/> or <see cref="RelationCount.One"/>.
    /// </summary>
    public string? OrderArray { get; set; }

    /// <summary>
    /// C# property name on the main BO that holds the BoId used in GetBo
    /// parameterised property access.
    /// Use <c>nameof(ThisModel.CustBoId)</c>.
    /// The resolved Opacc expression (e.g. <c>SalDoc.CustBoId</c>) is stored in
    /// <see cref="RelationMeta.GetBoId"/> for tooling and documentation.
    /// </summary>
    public string? GetBoId { get; set; }

    /// <summary>
    /// Optional raw additional filter conditions appended with <c>and</c>.
    /// For conditions that cannot be expressed as a typed property pair.
    /// Example: <c>"and Contact.IsMainContact = 1"</c>
    /// </summary>
    public string? AdditionalFilter { get; set; }

    /// <summary>The .NET type of the related BO.</summary>
    public Type RelatedType => typeof(TRelated);

    // ---------------------------------------------------------------
    // Constructors
    // ---------------------------------------------------------------

    /// <summary>
    /// Simple join — alias defaults to the BO entity name of <typeparamref name="TRelated"/>.
    /// </summary>
    public BoRelationAttribute(string relatedProperty, string mainProperty)
    {
        RelatedProperty = relatedProperty;
        MainProperty    = mainProperty;
    }

    /// <summary>
    /// Join with an explicit alias (required when the same BO appears twice on a model).
    /// </summary>
    public BoRelationAttribute(string alias, string relatedProperty, string mainProperty)
    {
        Alias           = alias;
        RelatedProperty = relatedProperty;
        MainProperty    = mainProperty;
    }

    // ---------------------------------------------------------------
    // Resolution (called by EntityMetadataCache)
    // ---------------------------------------------------------------

    public RelationMeta ToRelationMeta(Type mainType, string mainBoEntity,
        Dictionary<string, PropertyMeta> mainProperties)
    {
        var relatedBoAttr  = typeof(TRelated).GetCustomAttribute<BoAttribute>();
        string relatedBoName = relatedBoAttr?.EntityName ?? typeof(TRelated).Name;
        string alias         = Alias ?? relatedBoName;

        // Resolve the Opacc local names from the C# property names
        string relatedLocalName = ResolveLocalName(typeof(TRelated), RelatedProperty);
        string mainLocalName    = ResolveLocalName(mainType, MainProperty, mainProperties);

        // Build the join filter
        string filter = $"{alias}.{relatedLocalName} = {mainBoEntity}.{mainLocalName}";
        if (!string.IsNullOrWhiteSpace(AdditionalFilter))
            filter += $" {AdditionalFilter.Trim()}";

        // Build Query Related= string:  Alias,Source,Count,OrderArray,Filter
        string rawDef = $"{alias},{relatedBoName},{Count},{OrderArray ?? ""},{filter}";

        // Resolve GetBoId to full Opacc expression
        string? resolvedGetBoId = GetBoId is null ? null
            : ResolveOoExpression(mainType, GetBoId, mainBoEntity, mainProperties);

        return new RelationMeta(alias, relatedBoName, filter, Count.ToString(), OrderArray,
            resolvedGetBoId, rawDef);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Resolves a C# property name to its Opacc local field name
    /// (the part after the last dot in the OO expression, modifiers stripped).
    /// Falls back to the property name itself if no [BoProperty] is present.
    /// </summary>
    private static string ResolveLocalName(Type type, string clrPropertyName,
        Dictionary<string, PropertyMeta>? cachedProps = null)
    {
        string? ooExpr = null;

        if (cachedProps != null && cachedProps.TryGetValue(clrPropertyName, out var meta))
            ooExpr = meta.OoExpression;

        if (ooExpr == null)
        {
            var prop   = type.GetProperty(clrPropertyName);
            var ooAttr = prop?.GetCustomAttribute<BoPropertyAttribute>();
            ooExpr = ooAttr?.Expression ?? clrPropertyName;
        }

        // Strip prefix (e.g. "Cust.Number" → "Number")
        var dotIdx = ooExpr.LastIndexOf('.');
        var local  = dotIdx >= 0 ? ooExpr[(dotIdx + 1)..] : ooExpr;

        // Strip modifiers: !!, @@n, (param)
        local = local.Split('(')[0].TrimEnd('!', '@');
        return local;
    }

    /// <summary>
    /// Resolves a C# property name on the main type to its full Opacc expression.
    /// If the expression has no BO prefix, one is prepended.
    /// </summary>
    private static string ResolveOoExpression(Type mainType, string clrPropertyName,
        string mainBoEntity, Dictionary<string, PropertyMeta> mainProperties)
    {
        if (mainProperties.TryGetValue(clrPropertyName, out var meta))
        {
            var expr = meta.OoExpression;
            return expr.Contains('.') ? expr : $"{mainBoEntity}.{expr}";
        }

        var prop   = mainType.GetProperty(clrPropertyName);
        var ooAttr = prop?.GetCustomAttribute<BoPropertyAttribute>();
        var raw    = ooAttr?.Expression ?? clrPropertyName;
        return raw.Contains('.') ? raw : $"{mainBoEntity}.{raw}";
    }
}
