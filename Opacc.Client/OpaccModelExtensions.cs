using System.Linq.Expressions;
using System.Reflection;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.SaveBo;

namespace Opacc.Client;

public static class OpaccModelExtensions
{
    // ================================================================
    // Delete
    // ================================================================

    /// <summary>Deletes this model instance.</summary>
    public static Task<DeleteBoResult> DeleteAsync(
        this IOpaccModel model,
        CancellationToken ct = default)
    {
        var client = RequireClient(model, "delete");
        return DeleteCoreAsync(model, client, ct);
    }

    /// <summary>Deletes each model in the collection one by one.</summary>
    public static async Task<IReadOnlyList<DeleteBoResult>> DeleteAsync(
        this IEnumerable<IOpaccModel> models,
        CancellationToken ct = default)
    {
        var results = new List<DeleteBoResult>();
        foreach (var model in models)
            results.Add(await model.DeleteAsync(ct));
        return results;
    }

    // ================================================================
    // Save / Create / Update — single model, explicit properties
    // ================================================================

    /// <summary>
    /// Creates or updates this model (CreateOrUpdate).
    /// Pass property selectors to save only those fields; omit to save all writable properties.
    /// </summary>
    public static Task<SaveBoResult> CreateOrUpdateAsync<T>(
        this T model,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
        => SaveCoreAsync(model, SaveBoOperation.CreateOrUpdate, properties);

    /// <summary>
    /// Creates this model as a new record.
    /// Pass property selectors to include only those fields; omit to include all writable properties.
    /// </summary>
    public static Task<SaveBoResult> CreateAsync<T>(
        this T model,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
        => SaveCoreAsync(model, SaveBoOperation.Create, properties);

    /// <summary>
    /// Updates this existing model record.
    /// Pass property selectors to update only those fields; omit to update all writable properties.
    /// </summary>
    public static Task<SaveBoResult> UpdateAsync<T>(
        this T model,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
        => SaveCoreAsync(model, SaveBoOperation.Update, properties);

    // ================================================================
    // Save / Create / Update — collection, explicit properties
    // ================================================================

    /// <summary>Creates or updates each model (CreateOrUpdate) one by one.</summary>
    public static async Task<IReadOnlyList<SaveBoResult>> CreateOrUpdateAsync<T>(
        this IEnumerable<T> models,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
    {
        var results = new List<SaveBoResult>();
        foreach (var model in models)
            results.Add(await SaveCoreAsync(model, SaveBoOperation.CreateOrUpdate, properties));
        return results;
    }

    /// <summary>Creates each model as a new record one by one.</summary>
    public static async Task<IReadOnlyList<SaveBoResult>> CreateAsync<T>(
        this IEnumerable<T> models,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
    {
        var results = new List<SaveBoResult>();
        foreach (var model in models)
            results.Add(await SaveCoreAsync(model, SaveBoOperation.Create, properties));
        return results;
    }

    /// <summary>Updates each model record one by one.</summary>
    public static async Task<IReadOnlyList<SaveBoResult>> UpdateAsync<T>(
        this IEnumerable<T> models,
        params Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
    {
        var results = new List<SaveBoResult>();
        foreach (var model in models)
            results.Add(await SaveCoreAsync(model, SaveBoOperation.Update, properties));
        return results;
    }

    // ================================================================
    // Internals
    // ================================================================

    private static Task<DeleteBoResult> DeleteCoreAsync(IOpaccModel model, IOpaccClient client, CancellationToken ct)
    {
        var metadata = EntityMetadataCache.Get(model.GetType());

        if (metadata.IdProperties.Count == 0)
            throw new InvalidOperationException(
                $"Cannot delete {model.GetType().Name}: no [BoId] property found. " +
                "Mark the primary key with [BoId] or use DeleteBoAsync<T>().Start(...) directly.");

        var indexNo     = metadata.DefaultIndex;
        var keySegments = metadata.GetIndexProperties(indexNo);
        var startKeys   = string.Join(",", KeyExtractor.SegmentsFromModel(metadata, indexNo, model));

        return client.DeleteBoRawAsync(metadata.BoEntity, startKeys, indexNo, keySegments.Count, ct);
    }

    private static Task<SaveBoResult> SaveCoreAsync<T>(
        T model,
        SaveBoOperation operation,
        Expression<Func<T, object?>>[] properties)
        where T : class, IOpaccModel, new()
    {
        var client   = RequireClient(model, "save");
        var metadata = EntityMetadataCache.Get<T>();
        var type     = typeof(T);

        if (metadata.IdProperties.Count == 0)
            throw new InvalidOperationException(
                $"Cannot save {type.Name}: no [BoId] property found. " +
                "Mark the primary key with [BoId] or use SaveBoAsync<T>().Start(...) directly.");

        var indexNo     = metadata.DefaultIndex;
        var keySegments = metadata.GetIndexProperties(indexNo);

        // When no selectors are given → include all writable properties
        // When selectors are given    → include only those
        HashSet<string>? includeOnly = properties.Length > 0
            ? properties.Select(GetPropertyName).ToHashSet(StringComparer.Ordinal)
            : null;

        // Update locates the record by its key segments and must not reassign them.
        HashSet<string>? excludeKeys = operation == SaveBoOperation.Update
            ? keySegments.Select(p => p.ClrName).ToHashSet(StringComparer.Ordinal)
            : null;

        var assignments = BuildAssignments(model, metadata, type, includeOnly, excludeKeys);

        // Create sends no start key; Update / CreateOrUpdate locate the record by its key segments.
        var (startKeys, fixedSegs) = operation == SaveBoOperation.Create
            ? ("", 0)
            : (string.Join(",", KeyExtractor.SegmentsFromModel(metadata, indexNo, model)), keySegments.Count);

        return client.SaveBoRawAsync(
            metadata.BoEntity,
            startKeys,
            indexNo,
            operation,
            assignments,
            fixedSegs);
    }

    private static IReadOnlyList<string> BuildAssignments(
        IOpaccModel model,
        Opacc.Client.Metadata.EntityMetadata metadata,
        Type type,
        HashSet<string>? includeOnly,
        HashSet<string>? excludeKeys)
    {
        var assignments = new List<string>();

        foreach (var meta in metadata.Properties.Values)
        {
            if (includeOnly != null && !includeOnly.Contains(meta.ClrName)) continue;
            if (excludeKeys != null && excludeKeys.Contains(meta.ClrName)) continue;
            if (meta.IsVirtual || meta.IsQueryOnly) continue;
            if (meta.OoExpression.Any(c => c is '!' or '@' or '(')) continue;

            var prop = type.GetProperty(meta.ClrName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) continue;

            var value = prop.GetValue(model);
            assignments.Add($"{meta.OoExpression}=@{OpaccValueSerializer.Serialize(value, meta.DataType)}");
        }

        return assignments;
    }

    private static string GetPropertyName<T>(Expression<Func<T, object?>> expr)
    {
        Expression body = expr.Body;
        if (body is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            body = u.Operand;
        if (body is MemberExpression m)
            return m.Member.Name;
        throw new ArgumentException($"Expression '{expr}' does not refer to a property.");
    }

    private static IOpaccClient RequireClient(IOpaccModel model, string operation)
        => ModelClientRegistry.GetClient(model)
            ?? throw new InvalidOperationException(
                $"Cannot {operation} {model.GetType().Name}: no OpaccClient is available. " +
                "Ensure IOpaccClient is registered via AddOpaccClient() and has been resolved " +
                "at least once in the current async scope (it is set automatically on first DI resolution).");
}
