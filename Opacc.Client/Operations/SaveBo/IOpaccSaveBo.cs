using System.Linq.Expressions;
using Opacc.Client.Enums;

namespace Opacc.Client.Operations.SaveBo;

public interface IOpaccSaveBo<T>
    where T : class, IOpaccModel, new()
{
    // ── Operation ──────────────────────────────────────────────────────

    /// <summary>Create a new record. Fails if the key already exists.</summary>
    IOpaccSaveBo<T> Create();

    /// <summary>Update an existing record. Fails if the key does not exist.</summary>
    IOpaccSaveBo<T> Update();

    /// <summary>Update if found, create otherwise. This is the default.</summary>
    IOpaccSaveBo<T> CreateOrUpdate();

    // ── Key lookup ─────────────────────────────────────────────────────

    /// <summary>Key value to locate the record (required for Update / CreateOrUpdate).</summary>
    IOpaccSaveBo<T> Start(object value);

    /// <summary>
    /// Composes the start key from the model instance using the effective index's key properties
    /// (ordered by segment number, comma-separated for multi-segment indices).
    /// </summary>
    IOpaccSaveBo<T> Start(T model);

    /// <summary>
    /// Composes the start key from explicit segment values, in index-segment order, comma-joined.
    /// Use when the key cannot be derived from <c>Set(...)</c> — e.g. a BO whose leading segments
    /// must be supplied via a composite BoId field rather than the individual segment properties.
    /// Example: <c>.Start(salDocInternalNo, salDocItemNo, poolNo)</c>.
    /// </summary>
    IOpaccSaveBo<T> Start(params object[] segments);

    /// <summary>Search operator (default: Equal).</summary>
    IOpaccSaveBo<T> SearchOperator(SearchOperator op);

    /// <summary>Override the BO index used for the key lookup.</summary>
    IOpaccSaveBo<T> Index(int indexNo, int? fixedSegments = null);

    /// <summary>
    /// Number of leading start-key segments to fix (FixedSegsOfBoIndex), independent of the index.
    /// By default every supplied start-key segment is fixed; use this to fix fewer than supplied
    /// (e.g. supply 2 segments as the start point but fix only 1 to act on a whole range).
    /// </summary>
    IOpaccSaveBo<T> FixedSegments(int count);

    // ── Field assignments ──────────────────────────────────────────────

    /// <summary>Assign a value to a typed property.</summary>
    IOpaccSaveBo<T> Set<TValue>(Expression<Func<T, TValue>> property, TValue value);

    /// <summary>
    /// Assign several fields at once via an object initializer: <c>x => new T { A = ..., B = ... }</c>.
    /// Each member is treated like an individual <see cref="Set{TValue}"/> (feeds start-key derivation
    /// and operation routing). Only simple member assignments are supported; value expressions must not
    /// reference the lambda parameter.
    /// </summary>
    IOpaccSaveBo<T> Set(Expression<Func<T, T>> assignments);

    /// <summary>Assign a raw Opacc expression value. The value is passed as-is after '@'.</summary>
    IOpaccSaveBo<T> SetRaw(string ooExpression, string? value);

    /// <summary>
    /// Copy all writable properties from a model instance.
    /// Skips virtual, query-only, and complex OO expressions.
    /// </summary>
    IOpaccSaveBo<T> SetFrom(T model);

    /// <summary>Copy only the specified properties from a model instance.</summary>
    IOpaccSaveBo<T> SetFrom(T model, params Expression<Func<T, object?>>[] properties);

    /// <summary>
    /// Excludes the given properties from the field assignments even if they were set via
    /// <c>Set</c>/<c>SetFrom</c>. Use for system-assigned key fields (e.g. an auto-numbered key)
    /// that must locate the record but must not be written back, which would otherwise fail the save.
    /// </summary>
    IOpaccSaveBo<T> ExcludeFromAssignments(params Expression<Func<T, object?>>[] properties);

    // ── Filter ─────────────────────────────────────────────────────────

    /// <summary>Raw Opacc filter expression. Multiple calls are AND-combined.</summary>
    IOpaccSaveBo<T> Filter(string opaccFilter);

    /// <summary>Typed lambda filter. Multiple calls are AND-combined.</summary>
    IOpaccSaveBo<T> Where(Expression<Func<T, bool>> predicate);

    // ── Options ────────────────────────────────────────────────────────

    /// <summary>Override which attributes are returned per record.</summary>
    IOpaccSaveBo<T> ResultObject(string fields);

    /// <summary>Include SaveBoStateCd and SaveBoInfo in the result (default: true).</summary>
    IOpaccSaveBo<T> WithReport(bool withReport = true);

    // NOTE: SaveBo has no dry-run / test mode. The Opacc docs describe arg 7 as a test flag, but that
    // is a copy-paste artifact from DeleteBo. To verify a filtered batch before saving, run a read-only
    // GetBo with the same filter. (DeleteBo.Test() is genuine and stays.)

    /// <summary>Suppress F-Script execution by appending #NoScript.</summary>
    IOpaccSaveBo<T> NoScript(bool noScript = true);

    /// <summary>Use a user-specific session.</summary>
    IOpaccSaveBo<T> WithCredentials(int userId, string? password = null);

    /// <summary>Use BOF-Script connection instead of WebService.</summary>
    IOpaccSaveBo<T> UseBofScript(bool use = true);

    /// <summary>Execute the save.</summary>
    Task<SaveBoResult> ExecuteAsync(CancellationToken ct = default);
}
