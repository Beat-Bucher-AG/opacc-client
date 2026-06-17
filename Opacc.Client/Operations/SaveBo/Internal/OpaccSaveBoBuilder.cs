using System.Linq.Expressions;
using System.Reflection;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Session;
using Opacc.Client.Transport;

namespace Opacc.Client.Operations.SaveBo.Internal;

/// <summary>
/// A single captured field assignment. Non-raw entries carry the resolved <see cref="PropertyMeta"/>
/// and the boxed CLR value so they can feed BOTH start-key derivation and assignment serialization.
/// Raw entries (<c>SetRaw</c>) carry a pre-formatted string and never take part in start-key derivation.
/// </summary>
internal readonly record struct SaveBoFieldAssignment(
    PropertyMeta? Meta,
    string OoExpression,
    object? RawValue,
    string? RawString,
    bool IsRaw);

internal class OpaccSaveBoBuilder<T> : IOpaccSaveBo<T>
    where T : class, IOpaccModel, new()
{
    private readonly IOpaccTransport _transport;
    private readonly EntityMetadata _metadata;

    private SaveBoOperation _operation = SaveBoOperation.CreateOrUpdate;
    private object? _start;
    private SearchOperator _searchOp = Enums.SearchOperator.Equal;
    private int? _indexNo;
    private int? _fixedSegments;
    private readonly List<SaveBoFieldAssignment> _fields = [];
    private readonly HashSet<string> _excludeFromAssignments = new(StringComparer.Ordinal);
    private readonly List<string> _filters = [];
    private string? _resultObject;
    private bool _withReport = true;
    private bool _noScript;
    private bool _useBofScript;
    private SessionCredentials? _credentials;

    internal OpaccSaveBoBuilder(IOpaccTransport transport)
    {
        _transport = transport;
        _metadata  = EntityMetadataCache.Get<T>();
    }

    public IOpaccSaveBo<T> Create()         { _operation = SaveBoOperation.Create;         return this; }
    public IOpaccSaveBo<T> Update()         { _operation = SaveBoOperation.Update;         return this; }
    public IOpaccSaveBo<T> CreateOrUpdate() { _operation = SaveBoOperation.CreateOrUpdate; return this; }

    public IOpaccSaveBo<T> Start(object value)
    {
        _start = value;
        return this;
    }

    public IOpaccSaveBo<T> Start(T model)
    {
        _start = model;
        return this;
    }

    public IOpaccSaveBo<T> Start(params object[] segments)
    {
        _start = string.Join(",", segments.Select(KeyExtractor.FormatSegment));
        return this;
    }

    public IOpaccSaveBo<T> SearchOperator(SearchOperator op)
    {
        _searchOp = op;
        return this;
    }

    public IOpaccSaveBo<T> Index(int indexNo, int? fixedSegments = null)
    {
        _indexNo       = indexNo;
        _fixedSegments = fixedSegments;
        return this;
    }

    public IOpaccSaveBo<T> FixedSegments(int count)
    {
        _fixedSegments = count;
        return this;
    }

    public IOpaccSaveBo<T> Set<TValue>(Expression<Func<T, TValue>> property, TValue value)
    {
        var propName = GetPropertyName(property);

        if (!_metadata.Properties.TryGetValue(propName, out var meta))
            throw new ArgumentException($"Property '{propName}' not found in metadata for {typeof(T).Name}.");

        _fields.Add(new SaveBoFieldAssignment(meta, meta.OoExpression, value, null, IsRaw: false));
        return this;
    }

    public IOpaccSaveBo<T> Set(Expression<Func<T, T>> assignments)
    {
        if (assignments.Body is not MemberInitExpression init)
            throw new ArgumentException(
                $"Set(...) expects an object initializer, e.g. x => new {typeof(T).Name} {{ A = ..., B = ... }}.",
                nameof(assignments));

        var param = assignments.Parameters[0];
        foreach (var binding in init.Bindings)
        {
            if (binding is not MemberAssignment ma)
                throw new ArgumentException(
                    $"Set(...) supports only simple member assignments; '{binding.Member.Name}' is a " +
                    "nested or collection initializer.",
                    nameof(assignments));

            if (!_metadata.Properties.TryGetValue(ma.Member.Name, out var meta))
                throw new ArgumentException(
                    $"Property '{ma.Member.Name}' not found in metadata for {typeof(T).Name}.",
                    nameof(assignments));

            var value = EvaluateMemberValue(ma.Expression, param);
            _fields.Add(new SaveBoFieldAssignment(meta, meta.OoExpression, value, null, IsRaw: false));
        }

        return this;
    }

    public IOpaccSaveBo<T> SetRaw(string ooExpression, string? value)
    {
        _fields.Add(new SaveBoFieldAssignment(null, ooExpression, null, value ?? "", IsRaw: true));
        return this;
    }

    public IOpaccSaveBo<T> SetFrom(T model)
    {
        AddFieldsFromModel(model, _ => true);
        return this;
    }

    public IOpaccSaveBo<T> SetFrom(T model, params Expression<Func<T, object?>>[] properties)
    {
        var names = properties
            .Select(p => GetPropertyName(p))
            .ToHashSet(StringComparer.Ordinal);

        AddFieldsFromModel(model, name => names.Contains(name));
        return this;
    }

    public IOpaccSaveBo<T> ExcludeFromAssignments(params Expression<Func<T, object?>>[] properties)
    {
        foreach (var p in properties)
            _excludeFromAssignments.Add(GetPropertyName(p));
        return this;
    }

    public IOpaccSaveBo<T> Filter(string opaccFilter)
    {
        if (!string.IsNullOrWhiteSpace(opaccFilter))
            _filters.Add(opaccFilter);
        return this;
    }

    public IOpaccSaveBo<T> Where(Expression<Func<T, bool>> predicate)
    {
        _filters.Add(PredicateTranslator.Translate(predicate, _metadata));
        return this;
    }

    public IOpaccSaveBo<T> ResultObject(string fields)
    {
        _resultObject = fields;
        return this;
    }

    public IOpaccSaveBo<T> WithReport(bool withReport = true)
    {
        _withReport = withReport;
        return this;
    }

    public IOpaccSaveBo<T> NoScript(bool noScript = true)
    {
        _noScript = noScript;
        return this;
    }

    public IOpaccSaveBo<T> WithCredentials(int userId, string? password = null)
    {
        _credentials = SessionCredentials.ForUser(userId, password);
        return this;
    }

    public IOpaccSaveBo<T> UseBofScript(bool use = true)
    {
        _useBofScript = use;
        return this;
    }

    public async Task<SaveBoResult> ExecuteAsync(CancellationToken ct = default)
    {
        var effectiveIndexNo   = _indexNo ?? _metadata.DefaultIndex;
        var keySegments        = _metadata.GetIndexProperties(effectiveIndexNo);
        var keySegmentClrNames = keySegments.Select(p => p.ClrName).ToHashSet(StringComparer.Ordinal);

        // Structured Set / SetFrom values keyed by CLR name (last write wins). Raw sets never participate.
        var valuesByClrName = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in _fields)
            if (f.Meta is not null)
                valuesByClrName[f.Meta.ClrName] = f.RawValue;

        // ── Start key derivation. Precedence: explicit .Start wins, else derive from .Set values. ──
        string startKeys;
        int providedSegmentCount;

        if (_start is T model)
        {
            var segs = KeyExtractor.SegmentsFromModel(_metadata, effectiveIndexNo, model);
            startKeys = string.Join(",", segs);
            providedSegmentCount = segs.Count;
        }
        else if (_start is not null)
        {
            startKeys = _start.ToString() ?? "";
            providedSegmentCount = startKeys.Length == 0 ? 0 : startKeys.Split(',').Length;
        }
        else
        {
            var segs = KeyExtractor.LeadingSegmentsFromValues(_metadata, effectiveIndexNo, valuesByClrName);
            startKeys = string.Join(",", segs);
            providedSegmentCount = segs.Count;
        }

        // ── Operation-aware routing + guard ──
        bool excludeKeysFromAssignments;
        int fixedSegs;

        switch (_operation)
        {
            case SaveBoOperation.Create:
                if (_start is not null && startKeys.Length > 0)
                    throw new InvalidOperationException(
                        $"SaveBo {_metadata.BoEntity} (Create) must not receive a start key. " +
                        "Remove .Start(...) — for Create the key fields are sent as assignments — " +
                        "or switch to .Update() / .CreateOrUpdate().");
                startKeys = "";
                fixedSegs = 0;
                excludeKeysFromAssignments = false; // keys must be assignments so the new record gets them
                break;

            case SaveBoOperation.Update:
                GuardStartKey(effectiveIndexNo, keySegments, providedSegmentCount);
                fixedSegs = _fixedSegments ?? providedSegmentCount;
                excludeKeysFromAssignments = true;  // locate by key, mutate the remaining fields
                break;

            default: // CreateOrUpdate
                // Needs a start key to locate the record for the update path. Some BOs forbid setting
                // the individual key-segment fields and require an explicit .Start(...) (e.g. a BoId
                // that encodes the leading segments) — the guard surfaces a missing start key clearly.
                GuardStartKey(effectiveIndexNo, keySegments, providedSegmentCount);
                fixedSegs = _fixedSegments ?? providedSegmentCount;
                excludeKeysFromAssignments = false; // create-path needs the keys as assignments
                break;
        }

        var request = new OpaccSaveBoRequest
        {
            BoEntity           = _metadata.BoEntity,
            StartKeys          = startKeys,
            SearchOperator     = _searchOp,
            IndexNo            = effectiveIndexNo,
            Operation          = _operation,
            FixedSegsOfBoIndex = fixedSegs,
            WithReport         = _withReport,
            Filter             = BuildFilterString(),
            ResultObject       = _resultObject,
            Assignments        = BuildAssignments(excludeKeysFromAssignments, keySegmentClrNames),
            NoScript           = _noScript,
            UseBofScript       = _useBofScript,
        };

        var response = await _transport.SendSaveBoAsync(request, _credentials, ct);

        var records = _withReport
            ? OpaccResponseParser.ParseRecords(response, _metadata.BoEntity)
                .Select(r => new SaveBoRecord(r))
                .ToList()
            : (List<SaveBoRecord>)[];

        return new SaveBoResult(records);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void GuardStartKey(int indexNo, IReadOnlyList<PropertyMeta> keySegments, int providedSegmentCount)
    {
        if (providedSegmentCount > 0) return; // a (possibly partial) leading start key was supplied
        if (_start is not null)        return; // caller supplied an explicit start value
        if (_filters.Count > 0)        return; // record(s) located via .Where()/.Filter() instead

        var segNames = keySegments.Count > 0
            ? string.Join(", ", keySegments.Select(p => p.ClrName))
            : "(none defined)";

        throw new InvalidOperationException(
            $"SaveBo {_metadata.BoEntity} ({_operation}) needs a start key for index {indexNo}, " +
            $"but no key segment ({segNames}) was set. Set the leading key segment(s) via .Set(...), " +
            "or call .Start(...), or add .Where()/.Filter() to locate the record(s).");
    }

    private IReadOnlyList<string> BuildAssignments(bool excludeKeys, HashSet<string> keySegmentClrNames)
    {
        var result = new List<string>(_fields.Count);

        foreach (var f in _fields)
        {
            if (f.IsRaw)
            {
                result.Add($"{f.OoExpression}=@{f.RawString}");
                continue;
            }

            var clrName = f.Meta!.ClrName;
            if (_excludeFromAssignments.Contains(clrName)) continue;
            if (excludeKeys && keySegmentClrNames.Contains(clrName)) continue;

            result.Add($"{f.OoExpression}=@{OpaccValueSerializer.Serialize(f.RawValue, f.Meta.DataType)}");
        }

        return result;
    }

    private void AddFieldsFromModel(T model, Func<string, bool> includePredicate)
    {
        var type = typeof(T);

        foreach (var meta in _metadata.Properties.Values)
        {
            if (!includePredicate(meta.ClrName)) continue;
            if (meta.IsVirtual || meta.IsQueryOnly) continue;

            // Skip complex OO expressions that are read-only lookups
            if (meta.OoExpression.Any(c => c is '!' or '@' or '(')) continue;

            var prop = type.GetProperty(meta.ClrName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) continue;

            _fields.Add(new SaveBoFieldAssignment(meta, meta.OoExpression, prop.GetValue(model), null, IsRaw: false));
        }
    }

    private string? BuildFilterString()
    {
        if (_filters.Count == 0) return null;

        var parts = _filters.Select(f => FilterTranslator.Translate(f, _metadata)).ToList();

        return parts.Count == 1
            ? parts[0]
            : string.Join(" and ", parts.Select(p => $"({p})"));
    }

    /// <summary>
    /// Evaluates a member-assignment value expression from an object initializer. Constants are read
    /// directly; everything else is compiled and invoked. The value expression must not dereference
    /// the lambda parameter (it is invoked with a null instance).
    /// </summary>
    private static object? EvaluateMemberValue(Expression expr, ParameterExpression param) =>
        expr is ConstantExpression c
            ? c.Value
            : Expression.Lambda<Func<T, object?>>(Expression.Convert(expr, typeof(object)), param)
                .Compile()(default!);

    private static string GetPropertyName<TValue>(Expression<Func<T, TValue>> expr)
    {
        Expression body = expr.Body;
        if (body is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            body = u.Operand;
        if (body is MemberExpression m)
            return m.Member.Name;
        throw new ArgumentException($"Expression '{expr}' does not refer to a property.");
    }
}
