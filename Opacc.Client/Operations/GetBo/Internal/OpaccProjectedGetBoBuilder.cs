using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Mapping;
using Opacc.Client.Metadata;
using Opacc.Client.Operations.Pagination;
using Opacc.Client.Relations;

namespace Opacc.Client.Operations.GetBo.Internal;

internal class OpaccProjectedGetBoBuilder<T, TResult> : IOpaccProjectedGetBo<T, TResult>
    where T : class, IOpaccModel, new()
{
    private readonly OpaccGetBoBuilder<T> _inner;
    private readonly Func<T, TResult>? _selector;
    private readonly List<ProjectedSelectColumn>? _projectedColumns;

    internal OpaccProjectedGetBoBuilder(OpaccGetBoBuilder<T> inner, Expression<Func<T, TResult>> selectorExpr)
    {
        _inner = inner;

        // Check for language/currency modifiers in the selector
        _projectedColumns = ExpressionHelper.GetProjectedSelectColumns(selectorExpr);

        if (_projectedColumns != null)
        {
            // Direct-mapping mode: columns with per-property suffixes
            _inner.SetProjectedColumns(_projectedColumns);
        }
        else
        {
            // Fast path: no modifiers, use compiled selector T → TResult
            _selector = selectorExpr.Compile();
            var propertyNames = ExpressionHelper.GetSourcePropertyNames(selectorExpr);
            _inner.SetPropertyNames(propertyNames);
        }
    }

    public IOpaccProjectedGetBo<T, TResult> Start(object value) { _inner.Start(value); return this; }
    public IOpaccProjectedGetBo<T, TResult> SearchOperator(SearchOperator op) { _inner.SearchOperator(op); return this; }
    public IOpaccProjectedGetBo<T, TResult> Index(int indexNo, int? segment = null) { _inner.Index(indexNo, segment); return this; }
    public IOpaccProjectedGetBo<T, TResult> Filter(string opaccFilter) { _inner.Filter(opaccFilter); return this; }
    public IOpaccProjectedGetBo<T, TResult> Where(Expression<Func<T, bool>> predicate) { _inner.Where(predicate); return this; }
    public IOpaccProjectedGetBo<T, TResult> Take(int count) { _inner.Take(count); return this; }
    public IOpaccProjectedGetBo<T, TResult> Skip(int count) { _inner.Skip(count); return this; }
    public IOpaccProjectedGetBo<T, TResult> Limit(int count) { _inner.Limit(count); return this; }
    public IOpaccProjectedGetBo<T, TResult> Include(params RelationSpec<T>[] relations) { _inner.Include(relations); return this; }
    public IOpaccProjectedGetBo<T, TResult> Include(string alias) { _inner.Include(alias); return this; }
    public IOpaccProjectedGetBo<T, TResult> VirtualAttributes(string attributes) { _inner.VirtualAttributes(attributes); return this; }
    public IOpaccProjectedGetBo<T, TResult> WithCredentials(int userId, string? password = null) { _inner.WithCredentials(userId, password); return this; }
    public IOpaccProjectedGetBo<T, TResult> UseBofScript(bool use = true) { _inner.UseBofScript(use); return this; }
    public IOpaccProjectedGetBo<T, TResult> Cache(bool cache = true) { _inner.Cache(cache); return this; }

    public async Task<TResult?> FirstAsync(CancellationToken ct = default)
    {
        if (_projectedColumns != null)
        {
            var response = await _inner.ExecuteRawAsync(ct);
            var items = MapDirect(response);
            return items.FirstOrDefault();
        }

        var item = await _inner.FirstAsync(ct);
        if (item == null) return default;
        return _selector!(item);
    }

    public async Task<List<TResult>> ToListAsync(CancellationToken ct = default)
    {
        if (_projectedColumns != null)
        {
            var response = await _inner.ExecuteRawAsync(ct);
            return MapDirect(response);
        }

        var items = await _inner.ToListAsync(ct);
        return items.ConvertAll(item => _selector!(item));
    }

    public async Task<OpaccPage<TResult>> ToPageAsync(string? cursor = null, CancellationToken ct = default)
    {
        if (_projectedColumns != null)
            throw new NotSupportedException(
                "ToPageAsync() is not yet supported with language/currency modifiers in Select. " +
                "Use ToListAsync() with Take()/Skip() instead.");

        var page = await _inner.ToPageAsync(cursor, ct);
        return new OpaccPage<TResult>(page.Items.ConvertAll(item => _selector!(item)), page.NextCursor);
    }

    private List<TResult> MapDirect(object? response)
    {
        var metadata = _inner.GetMetadata();

        // For GetBo, response columns use the full OO expression (e.g., "Art.Name1@2")
        // Build mappings: OoExpression → TResult property name
        var mappings = _projectedColumns!.Select(col =>
        {
            var dataType = metadata.Properties.TryGetValue(col.ClrName, out var meta)
                ? meta.DataType
                : OpaccDataType.None;

            // Build the actual OO expression that was sent to the server
            var ooExpr = meta?.OoExpression ?? col.ClrName;
            if (!ooExpr.Contains('.'))
                ooExpr = metadata.BoEntity + "." + ooExpr;
            if (col.Suffix != null)
                ooExpr += col.Suffix;

            return (OoExpression: ooExpr, ResultPropertyName: col.Alias, DataType: dataType);
        }).ToList();

        return ResponseMapper.MapDirectToList<TResult>(response, metadata.BoEntity, mappings);
    }
}
