using System.Linq.Expressions;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Mapping;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Operations.Exceptions;
using Opacc.Client.Operations.Pagination;
using Opacc.Client.Session;
using Opacc.Client.Transport;
using OpaccWebservice;

namespace Opacc.Client.Operations.Query.Internal;

internal class OpaccQueryBuilder<T> : IOpaccQuery<T>
    where T : class, IOpaccModel, new()
{
    private readonly IOpaccTransport _transport;
    private readonly IOpaccClient? _client;
    private readonly EntityMetadata _metadata;

    private readonly List<string> _filters = new();
    private List<string>? _translatedFilters;
    private readonly List<string> _customRelations = new();
    private readonly List<string> _orderByClauses = new();
    private readonly List<string> _orderByAsDateClauses = new();
    private readonly List<string> _orderByAsNmbClauses = new();
    private readonly List<string> _defines = new();
    private List<string>? _selectPropertyNames;
    private Dictionary<string, string>? _selectSuffixes; // ClrName → suffix (e.g. "@2")
    private int? _maxRows;
    private bool? _distinct;
    private string? _scrolling;
    private bool _cache;
    private bool _useBofScript;
    private SessionCredentials? _credentials;
    private int _skip;
    private string[]? _redoData;
    private string? _redoArgs;

    internal OpaccQueryBuilder(IOpaccTransport transport, IOpaccClient? client = null)
    {
        _transport = transport;
        _client = client;
        _metadata = EntityMetadataCache.Get<T>();
    }

    // ================================================================
    // Fluent API
    // ================================================================

    public IOpaccQuery<T> Where(string opaccFilter)
    {
        if (!string.IsNullOrWhiteSpace(opaccFilter))
            _filters.Add(opaccFilter);
        return this;
    }

    public IOpaccQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        _filters.Add(PredicateTranslator.Translate(predicate, _metadata, isQuerySyntax: true));
        return this;
    }

    public IOpaccQuery<T> Where(Expression<Func<T, object>> property, string op, object value)
    {
        var propName = ExpressionHelper.GetPropertyName(property);

        if (!_metadata.Properties.TryGetValue(propName, out var propMeta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for {typeof(T).Name}");

        // OO-Expression mit BO-Prefix
        var ooExpr = propMeta.OoExpression;
        if (!ooExpr.Contains('.'))
            ooExpr = _metadata.BoEntity + "." + ooExpr;

        // Wert formatieren
        var formattedValue = FormatFilterValue(value, propMeta);

        _filters.Add($"{ooExpr} {op.Trim()} {formattedValue}");
        return this;
    }

    public IOpaccQuery<T> Select(params Expression<Func<T, object>>[] properties)
    {
        var columns = properties.Select(p => ExpressionHelper.ExtractSelectColumn(p.Body)).ToList();
        _selectPropertyNames = columns.Select(c => c.ClrName).ToList();
        var suffixes = columns.Where(c => c.Suffix != null).ToList();
        if (suffixes.Count > 0)
        {
            _selectSuffixes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in suffixes)
                _selectSuffixes[col.ClrName] = col.Suffix!;
        }
        return this;
    }

    public IOpaccProjectedQuery<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        return new OpaccProjectedQueryBuilder<T, TResult>(this, selector);
    }

    public IOpaccQuery<T> Related<TRelated>(
        RelationAlias<TRelated> alias,
        Expression<Func<TRelated, T, bool>> filter,
        RelationCount count = RelationCount.Default,
        string? orderArray = null)
        where TRelated : class, IOpaccModel, new()
    {
        var relatedMeta = EntityMetadataCache.Get<TRelated>();
        var filterStr = RelationFilterTranslator.Translate(filter, alias.Alias, relatedMeta, _metadata);

        var countStr = count switch
        {
            RelationCount.One   => "One",
            RelationCount.ToOne => "ToOne",
            RelationCount.First => "First",
            RelationCount.Last  => "Last",
            RelationCount.All   => "All",
            _                   => "",   // Default: leer = Opacc-Default (One)
        };

        // Format: Alias,Source,Count,OrderArray,FilterExpression
        _customRelations.Add($"{alias.Alias},{relatedMeta.BoEntity},{countStr},{orderArray ?? ""},{filterStr}");
        return this;
    }

    /// <summary>Setzt die Property-Namen direkt (intern, für Projected-Builder).</summary>
    internal void SetPropertyNames(List<string> names) => _selectPropertyNames = names;

    private List<ProjectedSelectColumn>? _projectedColumns;

    /// <summary>
    /// Sets projected columns with per-column language/currency suffixes.
    /// When set, BuildColumns uses these instead of the normal property names.
    /// </summary>
    internal void SetProjectedColumns(List<ProjectedSelectColumn> columns)
    {
        _projectedColumns = columns;
        _selectPropertyNames = columns.Select(c => c.ClrName).Distinct().ToList();
    }

    /// <summary>
    /// Executes the query and returns the raw response for direct mapping.
    /// Used by projected builders when modifiers are present.
    /// </summary>
    internal async Task<object?> ExecuteRawAsync(CancellationToken ct)
    {
        var propertyNames = _selectPropertyNames ?? _metadata.GetAllSelectablePropertyNames(includeQueryOnly: true);
        var effectiveMaxRows = _skip > 0
            ? (_maxRows.HasValue
                ? _skip + _maxRows.Value
                : throw new InvalidOperationException("Skip() requires Take() or Limit() to be set."))
            : _maxRows;
        var request = BuildRequest(propertyNames, effectiveMaxRows);
        return await _transport.SendQueryAsync(request, _credentials, ct);
    }

    internal EntityMetadata GetMetadata() => _metadata;

    public IOpaccQuery<T> Take(int count)
    {
        _maxRows = count;
        return this;
    }

    public IOpaccQuery<T> Skip(int count)
    {
        _skip = Math.Max(0, count);
        return this;
    }

    public IOpaccQuery<T> Limit(int count) => Take(count);

    public IOpaccQuery<T> OrderBy(Expression<Func<T, object>> property, bool descending = false)
    {
        var propName = ExpressionHelper.GetPropertyName(property);

        if (!_metadata.Properties.TryGetValue(propName, out var propMeta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for {typeof(T).Name}");

        var ooExpr = propMeta.OoExpression;
        if (!ooExpr.Contains('.'))
            ooExpr = _metadata.BoEntity + "." + ooExpr;

        _orderByClauses.Add(descending ? $"-{ooExpr}" : ooExpr);
        return this;
    }

    public IOpaccQuery<T> OrderBy(string opaccOrderBy)
    {
        if (!string.IsNullOrWhiteSpace(opaccOrderBy))
            _orderByClauses.Add(opaccOrderBy);
        return this;
    }

    public IOpaccQuery<T> OrderByAsDate(Expression<Func<T, object>> property, bool descending = false)
    {
        var propName = ExpressionHelper.GetPropertyName(property);

        if (!_metadata.Properties.TryGetValue(propName, out var propMeta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for {typeof(T).Name}");

        var ooExpr = propMeta.OoExpression;
        if (!ooExpr.Contains('.'))
            ooExpr = _metadata.BoEntity + "." + ooExpr;

        _orderByAsDateClauses.Add(descending ? $"-{ooExpr}" : ooExpr);
        return this;
    }

    public IOpaccQuery<T> OrderByAsNmb(Expression<Func<T, object>> property, bool descending = false)
    {
        var propName = ExpressionHelper.GetPropertyName(property);

        if (!_metadata.Properties.TryGetValue(propName, out var propMeta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for {typeof(T).Name}");

        var ooExpr = propMeta.OoExpression;
        if (!ooExpr.Contains('.'))
            ooExpr = _metadata.BoEntity + "." + ooExpr;

        _orderByAsNmbClauses.Add(descending ? $"-{ooExpr}" : ooExpr);
        return this;
    }

    public IOpaccQuery<T> Distinct(bool distinct = true)
    {
        _distinct = distinct;
        return this;
    }

    public IOpaccQuery<T> Define(string name, string expression)
    {
        var normalizedName = name.StartsWith('@') ? name : "@" + name;
        _defines.Add($"{normalizedName},{expression}");
        return this;
    }

    public IOpaccQuery<T> Scrolling(string scrollingToken)
    {
        _scrolling = scrollingToken;
        return this;
    }

    public IOpaccQuery<T> WithCredentials(int userId, string? password = null)
    {
        _credentials = SessionCredentials.ForUser(userId, password);
        return this;
    }

    public IOpaccQuery<T> UseBofScript(bool use = true)
    {
        _useBofScript = use;
        return this;
    }

    public IOpaccQuery<T> Cache(bool cache = true)
    {
        _cache = cache;
        return this;
    }

    // ================================================================
    // Execution
    // ================================================================

    public async Task<T?> FirstAsync(CancellationToken ct = default)
    {
        _maxRows = 1;
        var list = await ExecuteAsync<T>(ct);
        return list.FirstOrDefault();
    }

    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        return await ExecuteAsync<T>(ct);
    }

    public async Task<TResult?> FirstAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new()
    {
        _maxRows = 1;
        var list = await ExecuteAsync<TResult>(ct);
        return list.FirstOrDefault();
    }

    public async Task<List<TResult>> ToListAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new()
    {
        return await ExecuteAsync<TResult>(ct);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        // Für Count brauchen wir nur eine ID-Column und MaxRows=0
        // Primär: [BoId]-markierte Property; Fallback: erste selektierbare Property
        var idProp =
            _metadata.IdProperties.FirstOrDefault()
            ?? _metadata.Properties.Values.FirstOrDefault(p => !p.IsVirtual && !p.IsQueryOnly)
            ?? throw new InvalidOperationException($"No selectable property found on {typeof(T).Name} — cannot count");

        var countRequest = new OpaccQueryRequest
        {
            BoEntity = _metadata.BoEntity,
            Columns = new List<QueryColumn>
            {
                new QueryColumn
                {
                    Expression = idProp.OoExpression.Contains('.') ? idProp.OoExpression : _metadata.BoEntity + "." + idProp.OoExpression,
                    ClrPropertyName = idProp.ClrName,
                },
            },
            MaxRows = 0, // Opacc gibt nur den Count zurück
            Filter = BuildFilterString(),
            Relations = BuildRelationsForFilter(),
            Cache = _cache,
            UseBofScript = _useBofScript,
        };

        var response = await _transport.SendQueryAsync(countRequest, _credentials, ct);

        // Response parsen — bei MaxRows=0 gibt Opacc die Anzahl zurück
        return ResponseMapper.ParseCount(response);
    }

    // ================================================================
    // Request Building
    // ================================================================

    private async Task<List<TResult>> ExecuteAsync<TResult>(CancellationToken ct)
        where TResult : class, new()
    {
        var propertyNames = ResolvePropertyNames<TResult>();
        var effectiveMaxRows = _skip > 0
            ? (_maxRows.HasValue
                ? _skip + _maxRows.Value
                : throw new InvalidOperationException("Skip() requires Take() or Limit() to be set."))
            : _maxRows;
        var request = BuildRequest(propertyNames, effectiveMaxRows);
        var response = await _transport.SendQueryAsync(request, _credentials, ct);
        var items = ResponseMapper.MapQueryToList<T, TResult>(response, _metadata, propertyNames);
        var result = _skip > 0 ? items.GetRange(_skip, items.Count - _skip) : items;
        if (_client != null)
            foreach (var item in result.OfType<IOpaccModel>())
                ModelClientRegistry.Associate(item, _client);
        return result;
    }

    private List<string> ResolvePropertyNames<TResult>()
    {
        List<string> names;

        if (_selectPropertyNames != null && _selectPropertyNames.Count > 0)
        {
            names = new List<string>(_selectPropertyNames);
        }
        else if (typeof(TResult) != typeof(T))
        {
            names = ProjectionMapper.ResolvePropertyNames<T, TResult>(_metadata);
        }
        else
        {
            // Bei Query: auch QueryOnly-Properties einschliessen
            names = _metadata.GetAllSelectablePropertyNames(includeQueryOnly: true);
        }

        // Default-Properties immer hinzufügen
        foreach (var defaultProp in _metadata.DefaultProperties)
        {
            if (!names.Contains(defaultProp.ClrName))
                names.Add(defaultProp.ClrName);
        }

        return names;
    }

    private OpaccQueryRequest BuildRequest(List<string> propertyNames, int? maxRowsOverride = null)
    {
        // Columns bauen
        var columns = BuildColumns(propertyNames);

        // Filter zusammenbauen
        var filterString = BuildFilterString();

        // Relationen ermitteln — sowohl aus Columns als auch aus Filter
        var relations = BuildRelations(propertyNames);

        return new OpaccQueryRequest
        {
            BoEntity = _metadata.BoEntity,
            Columns = columns,
            Filter = filterString,
            MaxRows = maxRowsOverride ?? _maxRows,
            Relations = relations,
            OrderBy = _orderByClauses.Count > 0 ? _orderByClauses : null,
            OrderByAsDate = _orderByAsDateClauses.Count > 0 ? _orderByAsDateClauses : null,
            OrderByAsNmb = _orderByAsNmbClauses.Count > 0 ? _orderByAsNmbClauses : null,
            Defines = _defines.Count > 0 ? _defines : null,
            Distinct = _distinct,
            Scrolling = _scrolling,
            RedoData = _redoData,
            RedoArgs = _redoArgs,
            Cache = _cache,
            UseBofScript = _useBofScript,
        };
    }

    public async Task<OpaccPage<T>> ToPageAsync(string? cursor = null, CancellationToken ct = default)
    {
        return await ToPageInternalAsync<T>(cursor, ct);
    }

    public async Task<OpaccPage<TResult>> ToPageAsync<TResult>(string? cursor = null, CancellationToken ct = default)
        where TResult : class, new()
    {
        return await ToPageInternalAsync<TResult>(cursor, ct);
    }

    private async Task<OpaccPage<TResult>> ToPageInternalAsync<TResult>(string? cursor, CancellationToken ct)
        where TResult : class, new()
    {
        if (_orderByClauses.Count == 0 && _orderByAsDateClauses.Count == 0 && _orderByAsNmbClauses.Count == 0)
            throw new InvalidOperationException("ToPageAsync() requires at least one OrderBy() call. Opacc Query scrolling requires a sort order.");

        var pageSize = _maxRows ?? 25;
        _scrolling ??= "ne";

        if (cursor != null)
        {
            _redoData = PageCursor.DecodeRows(cursor);
            _redoArgs = $"ne,,{pageSize}";
        }

        var propertyNames = ResolvePropertyNames<TResult>();
        var request = BuildRequest(propertyNames, pageSize);

        FlatResponseData? response;
        try
        {
            response = await _transport.SendQueryAsync(request, _credentials, ct);
        }
        catch (OpaccRequestException ex) when (
            ex.MessageId.Contains("NoNext", StringComparison.OrdinalIgnoreCase) ||
            ex.MessageId.Contains("NoBoth", StringComparison.OrdinalIgnoreCase))
        {
            return new OpaccPage<TResult>(new List<TResult>(), null);
        }

        var items = ResponseMapper.MapQueryToList<T, TResult>(response, _metadata, propertyNames);
        if (_client != null)
            foreach (var item in items.OfType<IOpaccModel>())
                ModelClientRegistry.Associate(item, _client);

        string? nextCursor = null;
        if (items.Count >= pageSize)
        {
            var redoRows = OpaccResponseParser.ParseRedoData(response);
            if (redoRows != null)
                nextCursor = PageCursor.EncodeRows(redoRows);
        }

        return new OpaccPage<TResult>(items, nextCursor);
    }

    /// <summary>
    /// Baut die Column-Liste für den Query-Request.
    ///
    /// Für Query werden die OO-Expressions verwendet, aber:
    /// - Properties mit [OOQuery]-Variante nutzen diese
    /// - Einfache Properties bekommen den BO-Prefix
    /// - Virtuelle Attribute werden zu "Column=PropertyName=Expression"
    /// </summary>
    private List<QueryColumn> BuildColumns(List<string> propertyNames)
    {
        // Projected columns with modifiers → each member becomes its own column
        if (_projectedColumns != null)
            return BuildProjectedColumns();

        var columns = new List<QueryColumn>();

        foreach (var propName in propertyNames)
        {
            if (!_metadata.Properties.TryGetValue(propName, out var propMeta))
                continue;

            var ooExpr = propMeta.OoExpression;

            // Für Virtuelle Attribute: "PropertyName=VirtualExpression" als Column
            if (propMeta.IsVirtual && !string.IsNullOrWhiteSpace(propMeta.VirtualExpression))
            {
                columns.Add(
                    new QueryColumn
                    {
                        Expression = propMeta.ClrName + ", " + propMeta.VirtualExpression,
                        HasAlias = true,
                        ClrPropertyName = propMeta.ClrName,
                    }
                );
                continue;
            }

            // BO-Prefix hinzufügen wenn nötig
            if (!ooExpr.Contains('.'))
                ooExpr = _metadata.BoEntity + "." + ooExpr;

            // Per-property language/currency suffix (non-projected Select)
            if (_selectSuffixes != null && _selectSuffixes.TryGetValue(propName, out var suffix))
                ooExpr += suffix;

            columns.Add(
                new QueryColumn
                {
                    Expression = ooExpr,
                    HasAlias = false,
                    ClrPropertyName = propMeta.ClrName,
                }
            );
        }

        return columns;
    }

    /// <summary>
    /// Builds columns from projected columns with language/currency suffixes.
    /// Opacc Query column alias format: Column=Alias,Expression (alias first).
    /// </summary>
    private List<QueryColumn> BuildProjectedColumns()
    {
        var columns = new List<QueryColumn>();

        foreach (var col in _projectedColumns!)
        {
            // Column from a RelationAlias.Col() call
            if (col.RelationAlias != null && col.RelatedSourceType != null)
            {
                var relMeta = EntityMetadataCache.Get(col.RelatedSourceType);
                if (!relMeta.Properties.TryGetValue(col.ClrName, out var relPropMeta))
                    continue;

                var relOoExpr = relPropMeta.OoExpression;
                var dotIdx = relOoExpr.IndexOf('.');
                var shortExpr = dotIdx >= 0 ? relOoExpr[(dotIdx + 1)..] : relOoExpr;

                columns.Add(new QueryColumn
                {
                    Expression = col.Alias + ", " + col.RelationAlias + "." + shortExpr,
                    HasAlias = true,
                    ClrPropertyName = col.Alias,
                });
                continue;
            }

            if (!_metadata.Properties.TryGetValue(col.ClrName, out var propMeta))
                continue;

            var ooExpr = propMeta.OoExpression;
            if (!ooExpr.Contains('.'))
                ooExpr = _metadata.BoEntity + "." + ooExpr;

            if (col.Suffix != null)
            {
                // Aliased column: "Column=NameFR, Art.Name1@@2" (alias, expression)
                columns.Add(new QueryColumn
                {
                    Expression = col.Alias + ", " + ooExpr + col.Suffix,
                    HasAlias = true,
                    ClrPropertyName = col.Alias,
                });
            }
            else
            {
                // Normal column aliased for unambiguous mapping: "Column=PriceCHF, Art.SalPriceRel"
                columns.Add(new QueryColumn
                {
                    Expression = col.Alias + ", " + ooExpr,
                    HasAlias = true,
                    ClrPropertyName = col.Alias,
                });
            }
        }

        return columns;
    }

    /// <summary>
    /// Translates each raw filter once and caches the result for the lifetime of this builder.
    /// Called by both BuildFilterString and BuildRelationsForFilter to avoid double translation.
    /// </summary>
    private List<string> GetTranslatedFilters() =>
        _translatedFilters ??= _filters.ConvertAll(f => FilterTranslator.Translate(f, _metadata));

    /// <summary>
    /// Baut den kombinierten Filter-String.
    /// Mehrere Where()-Aufrufe werden mit AND verknüpft.
    /// Property-Tags {PropertyName} werden übersetzt.
    /// </summary>
    private string? BuildFilterString()
    {
        if (_filters.Count == 0)
            return null;

        var translated = GetTranslatedFilters();

        if (translated.Count == 1)
            return translated[0];

        return string.Join(" and ", translated.Select(p => $"({p})"));
    }

    /// <summary>
    /// Ermittelt alle benötigten Relationen aus den selektierten
    /// Properties UND aus den Filter-Expressions.
    /// </summary>
    private List<string>? BuildRelations(List<string> propertyNames)
    {
        var requiredRelations = new HashSet<string>();

        // Custom relations from .Related() calls
        foreach (var rel in _customRelations)
            requiredRelations.Add(rel);

        // Relationen aus selektierten Properties
        foreach (var rel in _metadata.GetRequiredRelations(propertyNames))
            requiredRelations.Add(rel.RawDefinition);

        // Relationen aus Filter-Expressions
        var filterRelations = BuildRelationsForFilter();
        if (filterRelations != null)
        {
            foreach (var rel in filterRelations)
                requiredRelations.Add(rel);
        }

        // Relationen aus OrderBy
        foreach (var orderBy in _orderByClauses)
        {
            var dotIndex = orderBy.IndexOf('.');
            if (dotIndex > 0)
            {
                var prefix = orderBy[..dotIndex].Trim();
                if (prefix != _metadata.BoEntity)
                {
                    var rel = _metadata.Relations.FirstOrDefault(r => r.Alias == prefix);
                    if (rel != null)
                        requiredRelations.Add(rel.RawDefinition);
                }
            }
        }

        return requiredRelations.Count > 0 ? requiredRelations.ToList() : null;
    }

    /// <summary>
    /// Ermittelt Relationen die in den Filter-Expressions benötigt werden.
    /// Parst die übersetzten Filter nach BO-Prefixes und matched sie gegen Relations.
    /// </summary>
    private List<string>? BuildRelationsForFilter()
    {
        if (_filters.Count == 0)
            return null;

        var relations = new List<string>();

        foreach (var translated in GetTranslatedFilters())
        {
            foreach (var rel in _metadata.Relations)
            {
                if (translated.Contains(rel.Alias + "."))
                    relations.Add(rel.RawDefinition);
            }
        }

        return relations.Count > 0 ? relations.Distinct().ToList() : null;
    }

    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Formatiert einen Wert für die Verwendung in einem Opacc-Filter.
    /// Strings werden in Quotes gewrappt, Booleans zu 0/1, etc.
    /// </summary>
    private static string FormatFilterValue(object value, PropertyMeta propMeta)
    {
        if (value == null)
            return "''";

        return value switch
        {
            string s => $"'{EscapeFilterString(s)}'",
            bool b => b ? "1" : "0",
            DateTime dt when propMeta.DataType == OpaccDataType.Date => $"{dt:dd.MM.yyyy}",
            DateTime dt => $"{dt:dd.MM.yyyy}",
            int or long or short or byte => value.ToString()!,
            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => $"'{EscapeFilterString(value.ToString()!)}'",
        };
    }

    private static string EscapeFilterString(string value)
    {
        // Opacc-Filter: Single Quotes escapen
        return value.Replace("'", "''");
    }
}
