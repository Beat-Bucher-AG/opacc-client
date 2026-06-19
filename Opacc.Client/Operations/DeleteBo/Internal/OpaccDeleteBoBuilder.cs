using System.Linq.Expressions;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Session;
using Opacc.Client.Transport;

namespace Opacc.Client.Operations.DeleteBo.Internal;

internal class OpaccDeleteBoBuilder<T> : IOpaccDeleteBo<T>
    where T : class, IOpaccModel, new()
{
    private readonly IOpaccTransport _transport;
    private readonly EntityMetadata _metadata;

    private object? _start;
    private SearchOperator _searchOp = Enums.SearchOperator.Equal;
    private int? _indexNo;
    private int? _fixedSegments;
    private readonly List<string> _filters = new();
    private bool _isTest;
    private bool _withReport = true;
    private bool _noScript;
    private bool _useBofScript;
    private string? _resultObject;
    private SessionCredentials? _credentials;

    internal OpaccDeleteBoBuilder(IOpaccTransport transport)
    {
        _transport = transport;
        _metadata = EntityMetadataCache.Get<T>();
    }

    public IOpaccDeleteBo<T> Start(object value)
    {
        _start = value;
        return this;
    }

    public IOpaccDeleteBo<T> Start(T model)
    {
        _start = model;
        return this;
    }

    public IOpaccDeleteBo<T> Start(params object[] segments)
    {
        _start = string.Join(",", segments.Select(KeyExtractor.FormatSegment));
        return this;
    }

    public IOpaccDeleteBo<T> SearchOperator(SearchOperator op)
    {
        _searchOp = op;
        return this;
    }

    public IOpaccDeleteBo<T> Index(int indexNo, int? fixedSegments = null)
    {
        _indexNo = indexNo;
        _fixedSegments = fixedSegments;
        return this;
    }

    public IOpaccDeleteBo<T> Filter(string opaccFilter)
    {
        if (!string.IsNullOrWhiteSpace(opaccFilter))
            _filters.Add(opaccFilter);
        return this;
    }

    public IOpaccDeleteBo<T> Where(Expression<Func<T, bool>> predicate)
    {
        _filters.Add(PredicateTranslator.Translate(predicate, _metadata));
        return this;
    }

    public IOpaccDeleteBo<T> Test(bool isTest = true)
    {
        _isTest = isTest;
        return this;
    }

    public IOpaccDeleteBo<T> WithReport(bool withReport = true)
    {
        _withReport = withReport;
        return this;
    }

    public IOpaccDeleteBo<T> NoScript(bool noScript = true)
    {
        _noScript = noScript;
        return this;
    }

    public IOpaccDeleteBo<T> ResultObject(string fields)
    {
        _resultObject = fields;
        return this;
    }

    public IOpaccDeleteBo<T> WithCredentials(int userId, string? password = null)
    {
        _credentials = SessionCredentials.ForUser(userId, password);
        return this;
    }

    public IOpaccDeleteBo<T> UseBofScript(bool use = true)
    {
        _useBofScript = use;
        return this;
    }

    public async Task<DeleteBoResult> ExecuteAsync(CancellationToken ct = default)
    {
        var effectiveIndexNo = _indexNo ?? _metadata.DefaultIndex;
        var request = new OpaccDeleteBoRequest
        {
            BoEntity = _metadata.BoEntity,
            StartKeys = BuildStartValue(effectiveIndexNo),
            SearchOperator = _searchOp,
            IndexNo = effectiveIndexNo,
            FixedSegsOfBoIndex = _fixedSegments ?? _metadata.GetIndexProperties(effectiveIndexNo).Count,
            IsTest = _isTest,
            WithReport = _withReport,
            Filter = BuildFilterString(),
            ResultObject = _resultObject,
            NoScript = _noScript,
            UseBofScript = _useBofScript,
        };

        var response = await _transport.SendDeleteBoAsync(request, _credentials, ct);

        var records = _withReport
            ? OpaccResponseParser.ParseRecords(response, _metadata.BoEntity)
                .Select(r => new DeleteBoRecord(r))
                .ToList()
            : (List<DeleteBoRecord>)[];

        return new DeleteBoResult(records, _isTest);
    }

    private string BuildStartValue(int effectiveIndexNo)
    {
        if (_start is null) return "";
        if (_start is not T model) return _start.ToString() ?? "";

        return string.Join(",", KeyExtractor.SegmentsFromModel(_metadata, effectiveIndexNo, model));
    }

    private string? BuildFilterString()
    {
        if (_filters.Count == 0) return null;

        var parts = _filters.Select(f => FilterTranslator.Translate(f, _metadata)).ToList();

        return parts.Count == 1
            ? parts[0]
            : string.Join(" and ", parts.Select(p => $"({p})"));
    }
}