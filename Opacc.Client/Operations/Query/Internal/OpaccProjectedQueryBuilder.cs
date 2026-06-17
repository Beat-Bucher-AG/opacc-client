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
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Operations.Pagination;
using Opacc.Client.Operations.Query;

namespace Opacc.Client.Operations.Query.Internal;

internal class OpaccProjectedQueryBuilder<T, TResult> : IOpaccProjectedQuery<T, TResult>
    where T : class, IOpaccModel, new()
{
    private readonly OpaccQueryBuilder<T> _inner;
    private readonly Func<T, TResult>? _selector;
    private readonly List<ProjectedSelectColumn>? _projectedColumns;

    internal OpaccProjectedQueryBuilder(OpaccQueryBuilder<T> inner, Expression<Func<T, TResult>> selectorExpr)
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

    public IOpaccProjectedQuery<T, TResult> Where(string opaccFilter) { _inner.Where(opaccFilter); return this; }
    public IOpaccProjectedQuery<T, TResult> Where(Expression<Func<T, bool>> predicate) { _inner.Where(predicate); return this; }
    public IOpaccProjectedQuery<T, TResult> Related<TRelated>(
        RelationAlias<TRelated> alias,
        Expression<Func<TRelated, T, bool>> filter,
        RelationCount count = RelationCount.Default,
        string? orderArray = null)
        where TRelated : class, IOpaccModel, new()
    { _inner.Related(alias, filter, count, orderArray); return this; }
    public IOpaccProjectedQuery<T, TResult> Take(int count) { _inner.Take(count); return this; }
    public IOpaccProjectedQuery<T, TResult> OrderBy(Expression<Func<T, object>> property, bool descending = false) { _inner.OrderBy(property, descending); return this; }
    public IOpaccProjectedQuery<T, TResult> OrderBy(string opaccOrderBy) { _inner.OrderBy(opaccOrderBy); return this; }
    public IOpaccProjectedQuery<T, TResult> OrderByAsDate(Expression<Func<T, object>> property, bool descending = false) { _inner.OrderByAsDate(property, descending); return this; }
    public IOpaccProjectedQuery<T, TResult> OrderByAsNmb(Expression<Func<T, object>> property, bool descending = false) { _inner.OrderByAsNmb(property, descending); return this; }
    public IOpaccProjectedQuery<T, TResult> Distinct(bool distinct = true) { _inner.Distinct(distinct); return this; }
    public IOpaccProjectedQuery<T, TResult> Define(string name, string expression) { _inner.Define(name, expression); return this; }
    public IOpaccProjectedQuery<T, TResult> Scrolling(string scrollingToken) { _inner.Scrolling(scrollingToken); return this; }
    public IOpaccProjectedQuery<T, TResult> Skip(int count) { _inner.Skip(count); return this; }
    public IOpaccProjectedQuery<T, TResult> Limit(int count) { _inner.Limit(count); return this; }
    public IOpaccProjectedQuery<T, TResult> WithCredentials(int userId, string? password = null) { _inner.WithCredentials(userId, password); return this; }
    public IOpaccProjectedQuery<T, TResult> UseBofScript(bool use = true) { _inner.UseBofScript(use); return this; }
    public IOpaccProjectedQuery<T, TResult> Cache(bool cache = true) { _inner.Cache(cache); return this; }

    public async Task<TResult?> FirstAsync(CancellationToken ct = default)
    {
        if (_projectedColumns != null)
        {
            _inner.Take(1);
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
        var mappings = _projectedColumns!.Select(col =>
        {
            OpaccDataType dataType;
            if (col.RelatedSourceType != null)
            {
                // Column from a RelationAlias.Col() — look up data type on the related entity
                var relMeta = EntityMetadataCache.Get(col.RelatedSourceType);
                dataType = relMeta.Properties.TryGetValue(col.ClrName, out var relPropMeta)
                    ? relPropMeta.DataType
                    : OpaccDataType.None;
            }
            else
            {
                dataType = metadata.Properties.TryGetValue(col.ClrName, out var propMeta)
                    ? propMeta.DataType
                    : OpaccDataType.None;
            }

            // The alias is both what the server returns and the TResult property name
            return (col.Alias, col.Alias, dataType);
        }).ToList();

        return ResponseMapper.MapDirectToList<TResult>(response, metadata.BoEntity, mappings);
    }
}
