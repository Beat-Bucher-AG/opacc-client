using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Mapping;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Session;
using Opacc.Client.Relations;
using Opacc.Client.Transport;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.Pagination;
using OpaccWebservice;

namespace Opacc.Client.Operations.GetBo.Internal;

internal class OpaccGetBoBuilder<T> : IOpaccGetBo<T>
    where T : class, IOpaccModel, new()
{
    private readonly IOpaccTransport _transport;
    private readonly IOpaccClient? _client;
    private readonly EntityMetadata _metadata;

    private object? _start;
    private SearchOperator _searchOp = Enums.SearchOperator.Equal;
    private int? _indexNo;
    private int? _segment;
    private readonly List<string> _filters = new();
    private readonly List<string> _includeAliases = new();
    private int? _count;
    private List<string>? _selectPropertyNames;
    private Dictionary<string, string>? _selectSuffixes; // ClrName → suffix (e.g. "@2")
    private string? _extraVirtualAttributes;
    private bool _cache;
    private bool _useBofScript;
    private SessionCredentials? _credentials;
    private int _skip;

    internal OpaccGetBoBuilder(IOpaccTransport transport, IOpaccClient? client = null)
    {
        _transport = transport;
        _client = client;
        _metadata = EntityMetadataCache.Get<T>();
    }

    // ================================================================
    // Fluent API
    // ================================================================

    public IOpaccGetBo<T> Start(object value)
    {
        _start = value;
        return this;
    }

    /// <summary>
    /// Composes the start key from the model instance using the effective index's key properties
    /// (ordered by segment number, comma-separated for multi-segment indices).
    /// </summary>
    public IOpaccGetBo<T> Start(T model)
    {
        _start = model;
        return this;
    }

    public IOpaccGetBo<T> SearchOperator(SearchOperator op)
    {
        _searchOp = op;
        return this;
    }

    public IOpaccGetBo<T> Index(int indexNo, int? segment = null)
    {
        _indexNo = indexNo;
        _segment = segment;
        return this;
    }

    public IOpaccGetBo<T> Filter(string opaccFilter)
    {
        if (!string.IsNullOrWhiteSpace(opaccFilter))
            _filters.Add(opaccFilter);
        return this;
    }

    public IOpaccGetBo<T> Where(Expression<Func<T, bool>> predicate)
    {
        _filters.Add(PredicateTranslator.Translate(predicate, _metadata));
        return this;
    }

    public IOpaccGetBo<T> Include(params RelationSpec<T>[] relations)
    {
        foreach (var r in relations)
            _includeAliases.Add(r.Alias);
        return this;
    }

    public IOpaccGetBo<T> Include(string alias)
    {
        if (!string.IsNullOrWhiteSpace(alias))
            _includeAliases.Add(alias);
        return this;
    }

    public IOpaccGetBo<T> Take(int count)
    {
        _count = count;
        return this;
    }

    public IOpaccGetBo<T> Skip(int count)
    {
        _skip = Math.Max(0, count);
        return this;
    }

    public IOpaccGetBo<T> Limit(int count) => Take(count);

    /// <summary>
    /// Felder-Selektion: Einzelne Lambdas (Lade-Hinweis, Ergebnis bleibt T).
    /// Beispiel: .Select(x => x.FullName, x => x.City)
    /// </summary>
    public IOpaccGetBo<T> Select(params Expression<Func<T, object>>[] properties)
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

    public IOpaccProjectedGetBo<T, TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        return new OpaccProjectedGetBoBuilder<T, TResult>(this, selector);
    }

    /// <summary>Setzt die Property-Namen direkt (intern, für Projected-Builder).</summary>
    internal void SetPropertyNames(List<string> names) => _selectPropertyNames = names;

    private List<ProjectedSelectColumn>? _projectedColumns;

    /// <summary>
    /// Sets projected columns with per-column language/currency suffixes.
    /// </summary>
    internal void SetProjectedColumns(List<ProjectedSelectColumn> columns)
    {
        _projectedColumns = columns;
        _selectPropertyNames = columns.Select(c => c.ClrName).Distinct().ToList();
    }

    /// <summary>
    /// Executes the GetBo and returns the raw response for direct mapping.
    /// </summary>
    internal async Task<object?> ExecuteRawAsync(CancellationToken ct)
    {
        var propertyNames = ResolvePropertyNames<T>();
        var request = BuildRequest(propertyNames, _count ?? 1);
        return await _transport.SendGetBoAsync(request, _credentials, ct);
    }

    internal EntityMetadata GetMetadata() => _metadata;

    public IOpaccGetBo<T> VirtualAttributes(string attributes)
    {
        _extraVirtualAttributes = attributes;
        return this;
    }

    public IOpaccGetBo<T> WithCredentials(int userId, string? password = null)
    {
        _credentials = SessionCredentials.ForUser(userId, password);
        return this;
    }

    public IOpaccGetBo<T> UseBofScript(bool use = true)
    {
        _useBofScript = use;
        return this;
    }

    public IOpaccGetBo<T> Cache(bool cache = true)
    {
        _cache = cache;
        return this;
    }

    // ================================================================
    // Execution
    // ================================================================

    public async Task<T?> FirstAsync(CancellationToken ct = default)
    {
        _count = 1;
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
        _count = 1;
        var list = await ExecuteAsync<TResult>(ct);
        return list.FirstOrDefault();
    }

    public async Task<List<TResult>> ToListAsync<TResult>(CancellationToken ct = default)
        where TResult : class, new()
    {
        return await ExecuteAsync<TResult>(ct);
    }

    // ================================================================
    // Request Building
    // ================================================================

    private async Task<List<TResult>> ExecuteAsync<TResult>(CancellationToken ct)
        where TResult : class, new()
    {
        var propertyNames = ResolvePropertyNames<TResult>();
        int? effectiveCount = _count;
        if (_skip > 0)
        {
            if (!_count.HasValue)
                throw new InvalidOperationException("Skip() requires Take() or Limit() to be set.");
            effectiveCount = _skip + _count.Value;
            if (effectiveCount > 99_999)
                throw new InvalidOperationException(
                    $"GetBo Skip+Take ({effectiveCount}) exceeds Opacc's NumberOfBos limit of 99999. " +
                    "Use ToPageAsync() for cursor-based pagination over large datasets.");
        }
        var request = BuildRequest(propertyNames, effectiveCount);
        var response = await _transport.SendGetBoAsync(request, _credentials, ct);
        var items = ResponseMapper.MapToList<T, TResult>(response, _metadata, propertyNames);
        var result = _skip > 0 ? items.Skip(_skip).ToList() : items;
        if (_client != null)
            foreach (var item in result.OfType<IOpaccModel>())
                ModelClientRegistry.Associate(item, _client);
        return result;
    }

    /// <summary>
    /// Bestimmt welche Properties geladen werden sollen.
    ///
    /// Reihenfolge:
    /// 1. Explizit via .Select() angegeben
    /// 2. Aus TResult (Projection-DTO) abgeleitet
    /// 3. Alle Properties (wenn weder Select noch Projection)
    ///
    /// Default-Properties ([OODefault]) werden immer hinzugefügt.
    /// </summary>
    private List<string> ResolvePropertyNames<TResult>()
    {
        List<string> names;

        if (_selectPropertyNames != null && _selectPropertyNames.Count > 0)
        {
            // Explizit selektiert
            names = new List<string>(_selectPropertyNames);
        }
        else if (typeof(TResult) != typeof(T))
        {
            // Projection: Properties von TResult auf T matchen
            names = ProjectionMapper.ResolvePropertyNames<T, TResult>(_metadata);
        }
        else
        {
            // Alles laden
            names = _metadata.GetAllSelectablePropertyNames(includeQueryOnly: false);
        }

        // Explizit includierte Relationen: alle deren Properties hinzufügen
        foreach (var alias in _includeAliases)
        {
            foreach (var prop in _metadata.Properties.Values)
            {
                if (prop.RelationAlias == alias && !names.Contains(prop.ClrName))
                    names.Add(prop.ClrName);
            }
        }

        // Default-Properties immer hinzufügen
        foreach (var defaultProp in _metadata.DefaultProperties)
        {
            if (!names.Contains(defaultProp.ClrName))
                names.Add(defaultProp.ClrName);
        }

        return names;
    }

    private string BuildSelectString(List<string> propertyNames)
    {
        // Projected columns with modifiers → each member becomes its own expression
        if (_projectedColumns != null)
            return BuildProjectedSelectString();

        if (_selectSuffixes == null || _selectSuffixes.Count == 0)
            return _metadata.BuildSelectString(propertyNames);

        var expressions = new List<string>();
        foreach (var name in propertyNames)
        {
            if (!_metadata.Properties.TryGetValue(name, out var meta) || meta.IsVirtual)
                continue;

            var expr = meta.OoExpression;
            if (!expr.Contains('.'))
                expr = _metadata.BoEntity + "." + expr;

            if (_selectSuffixes.TryGetValue(name, out var suffix))
                expr += suffix;

            expressions.Add(expr);
        }
        return string.Join(", ", expressions);
    }

    /// <summary>
    /// Builds select string for projected queries with language/currency modifiers.
    /// Each projected member gets its own OO expression with optional suffix.
    /// </summary>
    private string BuildProjectedSelectString()
    {
        var expressions = new List<string>();
        foreach (var col in _projectedColumns!)
        {
            if (!_metadata.Properties.TryGetValue(col.ClrName, out var meta) || meta.IsVirtual)
                continue;

            var expr = meta.OoExpression;
            if (!expr.Contains('.'))
                expr = _metadata.BoEntity + "." + expr;

            if (col.Suffix != null)
                expr += col.Suffix;

            expressions.Add(expr);
        }
        return string.Join(", ", expressions);
    }

    private string? BuildFilterString()
    {
        if (_filters.Count == 0)
            return null;

        var translatedParts = _filters
            .Select(f => FilterTranslator.Translate(f, _metadata))
            .ToList();

        if (translatedParts.Count == 1)
            return translatedParts[0];

        return string.Join(" and ", translatedParts.Select(p => $"({p})"));
    }

    private OpaccGetBoRequest BuildRequest(List<string> propertyNames, int? countOverride = null)
    {
        var effectiveIndexNo = _indexNo ?? _metadata.DefaultIndex;
        var indexProps = _metadata.GetIndexProperties(effectiveIndexNo);

        // Select-String bauen (mit optionalen Sprach-/Währungs-Suffixen)
        var selectString = BuildSelectString(propertyNames);

        // Virtual Attributes sammeln
        var virtualAttrs = _metadata.GetVirtualAttributes(propertyNames);
        if (!string.IsNullOrWhiteSpace(_extraVirtualAttributes))
            virtualAttrs.Add(_extraVirtualAttributes);

        return new OpaccGetBoRequest
        {
            BoEntity = _metadata.BoEntity,
            Start = BuildStartValue(effectiveIndexNo),
            SearchOperator = _searchOp,
            IndexNo = effectiveIndexNo,
            Segment = _segment ?? indexProps.Count,
            Count = countOverride ?? _count ?? 1,
            SelectAttributes = selectString,
            VirtualAttributes = virtualAttrs,
            Filter = BuildFilterString(),
            Cache = _cache,
            UseBofScript = _useBofScript,
        };
    }

    /// <summary>
    /// Resolves the Start key value.
    /// When <c>_start</c> is a model instance of type <typeparamref name="T"/>, reads the
    /// index key properties (ordered by segment) and composes them as a comma-separated string.
    /// Otherwise falls back to <c>_start.ToString()</c>.
    /// </summary>
    private string BuildStartValue(int effectiveIndexNo)
    {
        if (_start is null) return "";
        if (_start is not T model) return _start.ToString() ?? "";

        return string.Join(",", KeyExtractor.SegmentsFromModel(_metadata, effectiveIndexNo, model));
    }

    public async Task<OpaccPage<T>> ToPageAsync(string? cursor = null, CancellationToken ct = default)
        => await ToPageInternalAsync<T>(cursor, ct);

    public async Task<OpaccPage<TResult>> ToPageAsync<TResult>(string? cursor = null, CancellationToken ct = default)
        where TResult : class, new()
        => await ToPageInternalAsync<TResult>(cursor, ct);

    private async Task<OpaccPage<TResult>> ToPageInternalAsync<TResult>(string? cursor, CancellationToken ct)
        where TResult : class, new()
    {
        var pageSize = _count ?? 25;

        if (cursor != null)
        {
            _start = PageCursor.DecodeId(cursor);
            _searchOp = Enums.SearchOperator.Next;
        }

        var propertyNames = ResolvePropertyNames<TResult>();
        var request = BuildRequest(propertyNames, pageSize);
        var response = await _transport.SendGetBoAsync(request, _credentials, ct);
        var items = ResponseMapper.MapToList<T, TResult>(response, _metadata, propertyNames);
        if (_client != null)
            foreach (var item in items.OfType<IOpaccModel>())
                ModelClientRegistry.Associate(item, _client);

        string? nextCursor = null;
        if (items.Count >= pageSize)
        {
            var idProp = _metadata.IdProperties.FirstOrDefault();
            if (idProp != null)
            {
                var idColName = idProp.OoExpression.Contains('.')
                    ? idProp.OoExpression
                    : _metadata.BoEntity + "." + idProp.OoExpression;
                var lastId = OpaccResponseParser.GetLastRowValue(response, idColName);
                if (lastId != null)
                    nextCursor = PageCursor.EncodeId(lastId);
            }
        }

        return new OpaccPage<TResult>(items, nextCursor);
    }
}
