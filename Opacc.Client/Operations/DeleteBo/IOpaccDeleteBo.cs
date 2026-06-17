using System.Linq.Expressions;
using Opacc.Client.Enums;

namespace Opacc.Client.Operations.DeleteBo;

public interface IOpaccDeleteBo<T>
    where T : class, IOpaccModel, new()
{
    /// <summary>Primary key value to delete.</summary>
    IOpaccDeleteBo<T> Start(object value);

    /// <summary>
    /// Composes the start key from the model instance using the effective index's key properties
    /// (ordered by segment number, comma-separated for multi-segment indices).
    /// </summary>
    IOpaccDeleteBo<T> Start(T model);

    /// <summary>Search operator (default: Equal).</summary>
    IOpaccDeleteBo<T> SearchOperator(SearchOperator op);

    /// <summary>Override the BO index used for the key lookup.</summary>
    IOpaccDeleteBo<T> Index(int indexNo, int? fixedSegments = null);

    /// <summary>Raw Opacc filter expression. Multiple calls are AND-combined.</summary>
    IOpaccDeleteBo<T> Filter(string opaccFilter);

    /// <summary>Typed lambda filter. Multiple calls are AND-combined.</summary>
    IOpaccDeleteBo<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>Dry-run — validates without actually deleting.</summary>
    IOpaccDeleteBo<T> Test(bool isTest = true);

    /// <summary>Include a report of deleted records in the result (default: true).</summary>
    IOpaccDeleteBo<T> WithReport(bool withReport = true);

    /// <summary>Skip BO scripts during deletion.</summary>
    IOpaccDeleteBo<T> NoScript(bool noScript = true);

    /// <summary>
    /// Override which attributes are returned per record (maps to the ResultObject parameter).
    /// Defaults to BoId, BoNumber, BoName plus DeleteBoStateCd/DeleteBoInfo when WithReport is on.
    /// </summary>
    IOpaccDeleteBo<T> ResultObject(string fields);

    /// <summary>Use a user-specific session.</summary>
    IOpaccDeleteBo<T> WithCredentials(int userId, string? password = null);

    /// <summary>Use BOF-Script connection instead of WebService.</summary>
    IOpaccDeleteBo<T> UseBofScript(bool use = true);

    /// <summary>Execute the delete.</summary>
    Task<DeleteBoResult> ExecuteAsync(CancellationToken ct = default);
}
