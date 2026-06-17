using System.Globalization;
using System.Reflection;
using Opacc.Client.Enums;

namespace Opacc.Client.Metadata;

/// <summary>
/// Single source of truth for composing BO index start keys (Startpunkt) from index metadata.
/// Shared by the GetBo / DeleteBo / SaveBo builders and the model extension methods so that
/// the segment formatting and gap handling are identical everywhere.
///
/// Start keys are a SEARCH context — not a field assignment. Dates are therefore formatted as
/// <c>yyyyMMdd</c> / <c>yyyyMMddHHmmss</c> (matching <see cref="Helper.PredicateTranslator"/>),
/// NOT the <c>dd.MM.yyyy</c> assignment format produced by <see cref="Helper.OpaccValueSerializer"/>.
/// </summary>
internal static class KeyExtractor
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>Formats a single index-segment value for a start key, honouring the property's data type.</summary>
    public static string FormatSegment(object? value, PropertyMeta meta) =>
        value is DateTime dt
            ? dt.ToString(meta.DataType == OpaccDataType.Date ? "yyyyMMdd" : "yyyyMMddHHmmss",
                          CultureInfo.InvariantCulture)
            : FormatSegment(value);

    /// <summary>
    /// Formats a raw start-key segment value without property metadata (used by <c>.Start(params ...)</c>).
    /// DateTime is rendered as a date (yyyyMMdd); pass a pre-formatted string for time components.
    /// </summary>
    public static string FormatSegment(object? value) => value switch
    {
        null           => "",
        string s       => s,
        bool b         => b ? "1" : "0",
        DateTime dt    => dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _              => value.ToString() ?? "",
    };

    /// <summary>
    /// Composes ALL key segments of the given index from a model instance (ordered by segment).
    /// Missing values become empty segments. Used by the <c>.Start(model)</c> path of every builder.
    /// </summary>
    public static IReadOnlyList<string> SegmentsFromModel(EntityMetadata metadata, int indexNo, object model)
    {
        var indexProps = metadata.GetIndexProperties(indexNo);
        if (indexProps.Count == 0) return [];

        var type = model.GetType();
        var parts = new List<string>(indexProps.Count);
        foreach (var prop in indexProps)
        {
            var clrProp = type.GetProperty(prop.ClrName, PublicInstance);
            parts.Add(FormatSegment(clrProp?.GetValue(model), prop));
        }
        return parts;
    }

    /// <summary>
    /// Composes the LEADING start-key segments from explicitly provided values (e.g. SaveBo
    /// <c>.Set(...)</c> calls keyed by CLR property name). Walks the index segments in order and
    /// collects each provided value up to the first missing segment, returning only that contiguous
    /// leading prefix. A non-leading index segment that is set without its predecessors is NOT part
    /// of the start key (it is treated purely as a field value) — so the result may be empty.
    /// </summary>
    public static IReadOnlyList<string> LeadingSegmentsFromValues(
        EntityMetadata metadata,
        int indexNo,
        IReadOnlyDictionary<string, object?> valuesByClrName)
    {
        var indexProps = metadata.GetIndexProperties(indexNo);
        if (indexProps.Count == 0) return [];

        var leading = new List<string>();
        foreach (var prop in indexProps)
        {
            if (!valuesByClrName.TryGetValue(prop.ClrName, out var value))
                break; // only a contiguous leading prefix is a valid start key
            leading.Add(FormatSegment(value, prop));
        }

        return leading;
    }
}
