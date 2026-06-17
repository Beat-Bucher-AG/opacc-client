using System.Linq.Expressions;
using Opacc.Client.Operations.Pagination;

namespace Opacc.Client.Operations.Query;

/// <summary>
/// Fluent builder für einen Query, dessen Ergebnis durch einen Selector-Ausdruck
/// auf <typeparamref name="TResult"/> projiziert wird.
/// Unterstützt anonyme Typen und DTOs ohne Parameterlos-Konstruktor-Einschränkung.
/// </summary>
public interface IOpaccProjectedQuery<T, TResult>
    where T : class, IOpaccModel, new()
{
    IOpaccProjectedQuery<T, TResult> Where(string opaccFilter);
    IOpaccProjectedQuery<T, TResult> Where(Expression<Func<T, bool>> predicate);
    IOpaccProjectedQuery<T, TResult> Related<TRelated>(
        RelationAlias<TRelated> alias,
        Expression<Func<TRelated, T, bool>> filter,
        RelationCount count = RelationCount.Default,
        string? orderArray = null)
        where TRelated : class, IOpaccModel, new();
    IOpaccProjectedQuery<T, TResult> Take(int count);
    IOpaccProjectedQuery<T, TResult> OrderBy(Expression<Func<T, object>> property, bool descending = false);
    IOpaccProjectedQuery<T, TResult> OrderBy(string opaccOrderBy);
    IOpaccProjectedQuery<T, TResult> OrderByAsDate(Expression<Func<T, object>> property, bool descending = false);
    IOpaccProjectedQuery<T, TResult> OrderByAsNmb(Expression<Func<T, object>> property, bool descending = false);
    IOpaccProjectedQuery<T, TResult> Distinct(bool distinct = true);
    IOpaccProjectedQuery<T, TResult> Define(string name, string expression);
    IOpaccProjectedQuery<T, TResult> Scrolling(string scrollingToken);
    IOpaccProjectedQuery<T, TResult> Skip(int count);
    IOpaccProjectedQuery<T, TResult> Limit(int count);
    IOpaccProjectedQuery<T, TResult> WithCredentials(int userId, string? password = null);
    IOpaccProjectedQuery<T, TResult> UseBofScript(bool use = true);
    IOpaccProjectedQuery<T, TResult> Cache(bool cache = true);

    Task<TResult?> FirstAsync(CancellationToken ct = default);
    Task<List<TResult>> ToListAsync(CancellationToken ct = default);
    Task<OpaccPage<TResult>> ToPageAsync(string? cursor = null, CancellationToken ct = default);
}
