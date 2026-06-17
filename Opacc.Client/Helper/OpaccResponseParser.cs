using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Operations.Exceptions;
using OpaccWebservice;

namespace Opacc.Client.Helper;

internal static class OpaccResponseParser
{
    /// <summary>
    /// Parst die FlatResponseData des Opacc-Backends in eine Liste von Records.
    ///
    /// FlatResponseData ist ein Column-Store:
    ///   Columns[0] = { Name: "Addr.Number",   Rows: ["1001", "1002", "1003"] }
    ///   Columns[1] = { Name: "Addr.FullName", Rows: ["Hans", "Peter", "Anna"] }
    ///
    /// Wird transponiert zu Row-Records:
    ///   [0] = { "Addr.Number": "1001", "Addr.FullName": "Hans" }
    ///   [1] = { "Addr.Number": "1002", "Addr.FullName": "Peter" }
    ///   [2] = { "Addr.Number": "1003", "Addr.FullName": "Anna" }
    /// </summary>
    public static List<Dictionary<string, object?>> ParseRecords(object? responseData, string boEntity)
    {
        if (responseData == null)
            return new List<Dictionary<string, object?>>();

        // FlatResponseData direkt
        if (responseData is FlatResponseData flatResponse)
            return ParseFlatResponseData(flatResponse);

        // FlatRequestResponse (der Wrapper)
        if (responseData is FlatRequestResponse flatRequestResponse)
        {
            if (flatRequestResponse.ResponseInfo != null && !flatRequestResponse.ResponseInfo.Successful)
            {
                throw new OpaccRequestException(flatRequestResponse.ResponseInfo.MessageId, flatRequestResponse.ResponseInfo.MlsMessageText);
            }

            if (flatRequestResponse.ResponseData != null)
                return ParseFlatResponseData(flatRequestResponse.ResponseData);

            return new List<Dictionary<string, object?>>();
        }

        // Fallback: versuchen zu casten
        throw new InvalidOperationException(
            $"Unexpected response type: {responseData.GetType().FullName}. " + $"Expected FlatResponseData or FlatRequestResponse."
        );
    }

    /// <summary>
    /// Transponiert den Column-Store in Row-Records.
    /// </summary>
    private static List<Dictionary<string, object?>> ParseFlatResponseData(FlatResponseData data)
    {
        var records = new List<Dictionary<string, object?>>();

        if (data.Columns == null || data.Columns.Length == 0 || data.RowCount == 0)
            return records;

        // Transponieren: Column-Store → Row-Records
        for (int rowIndex = 0; rowIndex < data.RowCount; rowIndex++)
        {
            var record = new Dictionary<string, object?>(data.ColumnCount);

            foreach (var column in data.Columns)
            {
                if (column?.Name == null || column.Rows == null)
                    continue;

                // Sicherheitscheck: Row-Index innerhalb des Arrays
                string? value = rowIndex < column.Rows.Length ? column.Rows[rowIndex] : null;

                record[column.Name] = value;

                // Zusätzlich den Kurznamen registrieren für flexibles Matching
                // "Addr.Number" → auch unter "Number" findbar
                // "Addr.Name1@2" → auch unter "Name1" findbar (Modifier stripped)
                var dotIndex = column.Name.LastIndexOf('.');
                if (dotIndex > 0 && dotIndex < column.Name.Length - 1)
                {
                    var shortName = column.Name[(dotIndex + 1)..];
                    record.TryAdd(shortName, value);

                    // Auch ohne Modifier-Suffix registrieren
                    var strippedName = StripModifierSuffix(shortName);
                    if (strippedName != shortName)
                        record.TryAdd(strippedName, value);
                }
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Parst die Anzahl Datensätze aus einer Count-Response (MaxRows=0).
    /// Bei MaxRows=0 gibt Opacc RowCount zurück ohne tatsächliche Daten.
    /// </summary>
    public static int ParseCount(object? responseData)
    {
        if (responseData == null)
            return 0;

        if (responseData is FlatResponseData flatResponse)
            return flatResponse.RowCount;

        if (responseData is FlatRequestResponse flatRequestResponse)
            return flatRequestResponse.ResponseData?.RowCount ?? 0;

        if (int.TryParse(responseData.ToString(), out var result))
            return result;

        return 0;
    }

    /// <summary>
    /// Parst die FlatResponseData in eine Liste von String-Records (für RawServiceResponse).
    /// </summary>
    public static List<Dictionary<string, string?>> ParseRecordsAsStrings(FlatResponseData? data)
    {
        var records = new List<Dictionary<string, string?>>();
        if (data?.Columns == null || data.Columns.Length == 0 || data.RowCount == 0)
            return records;

        for (int i = 0; i < data.RowCount; i++)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in data.Columns)
            {
                if (col?.Name == null) continue;
                var value = i < (col.Rows?.Length ?? 0) ? col.Rows![i] : null;
                row.TryAdd(col.Name, value);
                var dot = col.Name.LastIndexOf('.');
                if (dot >= 0)
                    row.TryAdd(col.Name[(dot + 1)..], value);
            }
            records.Add(row);
        }

        return records;
    }

    /// <summary>
    /// Prüft die ResponseInfo auf Fehler und wirft eine Exception falls nötig.
    /// </summary>
    public static void ThrowIfError(ResponseInfo? info)
    {
        if (info != null && !info.Successful)
        {
            throw new OpaccRequestException(info.MessageId, info.MlsMessageText);
        }
    }

    /// <summary>
    /// Extracts the RedoData column rows from the response (used for Query cursor pagination).
    /// Opacc returns this as column name "RedoData" (in the WCF response, "#RedoData" in parameters).
    /// Returns null if no RedoData column is present.
    /// </summary>
    public static string[]? ParseRedoData(FlatResponseData? data)
    {
        if (data?.Columns == null) return null;
        // Opacc uses "#RedoData" as the Query parameter name, but the WCF response Column.Name
        // is "RedoData" (XML element names cannot contain #). Check both for robustness.
        var col = Array.Find(data.Columns, c => c?.Name == "RedoData" || c?.Name == "#RedoData");
        return col?.Rows is { Length: > 0 } ? col.Rows : null;
    }

    /// <summary>
    /// Gets the last row's value for a given column name (used for GetBo cursor).
    /// </summary>
    public static string? GetLastRowValue(FlatResponseData? data, string columnName)
    {
        if (data == null || data.RowCount == 0) return null;
        var col = Array.Find(data.Columns, c => c?.Name == columnName);
        if (col?.Rows == null || col.Rows.Length == 0) return null;
        var lastIndex = Math.Min(data.RowCount - 1, col.Rows.Length - 1);
        return col.Rows[lastIndex];
    }

    /// <summary>
    /// Strips language (@2, @@2) and currency ($7, $$7) suffixes from a column short name.
    /// "Name1@2" → "Name1", "Price1$$7" → "Price1", "Number" → "Number" (unchanged).
    /// </summary>
    internal static string StripModifierSuffix(string name)
    {
        // Find the first @ or $ that starts a modifier suffix
        for (int i = 0; i < name.Length; i++)
        {
            if (name[i] is '@' or '$')
            {
                // Verify the rest is @digits or @@digits / $digits or $$digits
                var rest = name[i..];
                if (rest.Length >= 2 && IsModifierSuffix(rest))
                    return name[..i];
            }
        }
        return name;
    }

    private static bool IsModifierSuffix(ReadOnlySpan<char> s)
    {
        // Matches: @digits, @@digits, $digits, $$digits
        var marker = s[0]; // @ or $
        int start = 1;
        if (s.Length > 1 && s[1] == marker)
            start = 2;
        if (start >= s.Length) return false;
        for (int i = start; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) return false;
        }
        return true;
    }
}
