using System.Linq.Expressions;
using Opacc.Client.Enums;
using Opacc.Client.Operations.Pagination;
using Opacc.Client.Relations;

namespace Opacc.Client.Operations.GetBo;

/// <summary>
/// Fluent builder für einen GetBo, dessen Ergebnis durch einen Selector-Ausdruck
/// auf <typeparamref name="TResult"/> projiziert wird.
/// Unterstützt anonyme Typen und DTOs ohne Parameterlos-Konstruktor-Einschränkung.
/// </summary>
public interface IOpaccProjectedGetBo<T, TResult>
    where T : class, IOpaccModel, new()
{
    IOpaccProjectedGetBo<T, TResult> Start(object value);
    IOpaccProjectedGetBo<T, TResult> SearchOperator(SearchOperator op);
    IOpaccProjectedGetBo<T, TResult> Index(int indexNo, int? segment = null);
    IOpaccProjectedGetBo<T, TResult> Filter(string opaccFilter);
    IOpaccProjectedGetBo<T, TResult> Where(Expression<Func<T, bool>> predicate);
    IOpaccProjectedGetBo<T, TResult> Take(int count);
    IOpaccProjectedGetBo<T, TResult> Skip(int count);
    IOpaccProjectedGetBo<T, TResult> Limit(int count);
    IOpaccProjectedGetBo<T, TResult> Include(params RelationSpec<T>[] relations);
    IOpaccProjectedGetBo<T, TResult> Include(string alias);
    IOpaccProjectedGetBo<T, TResult> VirtualAttributes(string attributes);
    IOpaccProjectedGetBo<T, TResult> WithCredentials(int userId, string? password = null);
    IOpaccProjectedGetBo<T, TResult> UseBofScript(bool use = true);
    IOpaccProjectedGetBo<T, TResult> Cache(bool cache = true);

    Task<TResult?> FirstAsync(CancellationToken ct = default);
    Task<List<TResult>> ToListAsync(CancellationToken ct = default);
    Task<OpaccPage<TResult>> ToPageAsync(string? cursor = null, CancellationToken ct = default);
}
