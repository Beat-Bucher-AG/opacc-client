using OpaccWebservice;

namespace Opacc.Client.Tests.Helpers;

/// <summary>
/// Builds <see cref="FlatResponseData"/> objects for unit tests,
/// mimicking the column-store format returned by the Opacc WCF service.
/// </summary>
internal static class FlatResponseBuilder
{
    public static FlatResponseData Empty() => new()
    {
        RowCount = 0,
        ColumnCount = 0,
        Columns = [],
    };

    /// <summary>
    /// Builds a column-store response from named columns.
    /// Each column has a name (OO expression) and one value per row.
    /// All columns must have the same number of values.
    /// </summary>
    public static FlatResponseData FromColumns(params (string name, string[] rows)[] columns)
    {
        var rowCount = columns.Length > 0 ? columns[0].rows.Length : 0;
        return new FlatResponseData
        {
            RowCount = rowCount,
            ColumnCount = columns.Length,
            Columns = columns
                .Select(c => new Column { Name = c.name, Rows = c.rows })
                .ToArray(),
        };
    }

    /// <summary>
    /// Builds a single-row response from name/value pairs.
    /// </summary>
    public static FlatResponseData SingleRow(params (string name, string value)[] fields) =>
        FromColumns(fields.Select(f => (f.name, new[] { f.value })).ToArray());

    /// <summary>
    /// Appends a #RedoData column to an existing response (simulates Query cursor pagination).
    /// </summary>
    public static FlatResponseData WithRedoData(FlatResponseData data, string[] redoRows)
    {
        var cols = (data.Columns ?? []).ToList();
        cols.Add(new Column { Name = "#RedoData", Rows = redoRows });
        return new FlatResponseData
        {
            RowCount = data.RowCount,
            ColumnCount = cols.Count,
            Columns = cols.ToArray(),
        };
    }

    /// <summary>
    /// Builds a response with a specific RowCount and no data columns (simulates MaxRows=0 count response).
    /// </summary>
    public static FlatResponseData CountOnly(int rowCount) => new()
    {
        RowCount = rowCount,
        ColumnCount = 0,
        Columns = [],
    };
}
