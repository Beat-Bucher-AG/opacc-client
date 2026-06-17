namespace Opacc.Client;

/// <summary>
/// Response from a raw service call via <see cref="IOpaccClient.SendRawAsync"/>.
/// Provides simple access to the flat response data returned by Opacc.
/// </summary>
public sealed class RawServiceResponse
{
    private readonly List<Dictionary<string, string?>> _records;

    internal RawServiceResponse(List<Dictionary<string, string?>> records)
        => _records = records;

    /// <summary>Number of result rows.</summary>
    public int Count => _records.Count;

    /// <summary>True if the response contains no rows.</summary>
    public bool IsEmpty => _records.Count == 0;

    /// <summary>
    /// Single scalar value — first column of the first row.
    /// Useful for services that return exactly one value (e.g. a price or a status code).
    /// </summary>
    public string? Scalar => _records.Count > 0 ? _records[0].Values.FirstOrDefault() : null;

    /// <summary>All result rows as key-value dictionaries (column name → value).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string?>> Records
        => _records.Select(r => (IReadOnlyDictionary<string, string?>)r).ToList();

    /// <summary>Gets a value by row index and column name. Returns null if not found.</summary>
    public string? this[int row, string column]
        => row < _records.Count && _records[row].TryGetValue(column, out var v) ? v : null;

    /// <summary>Gets all values of a named column across all rows.</summary>
    public IReadOnlyList<string?> Column(string name)
        => _records.Select(r => r.TryGetValue(name, out var v) ? v : null).ToList();
}
