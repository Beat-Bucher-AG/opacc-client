using Opacc.Client.Enums;

namespace Opacc.Client.CLI.Scaffold;

/// <summary>
/// Bildet Opacc DataTypeCd + Format auf C#-Typen ab.
///
/// DataTypeCd:
///   A = Alpha/String
///   N = Numeric  (Format "8.0" → int, "8.2" → decimal)
///   D = Date
///   B = Boolean
///   T = Time/DateTime
/// </summary>
internal static class TypeMapper
{
    public static MappedType Map(string dataTypeCd, string? format = null)
    {
        return dataTypeCd.Trim().ToUpperInvariant() switch
        {
            "A"  => new("string", null, false),
            "D"  => new("DateTime", OpaccDataType.Date, true),
            "B"  => new("bool", null, true),
            "N"  => MapNumeric(format),
            "T"  => new("DateTime", null, true),
            _    => new("string", null, false),   // Fallback
        };
    }

    private static MappedType MapNumeric(string? format)
    {
        // Format: "8.0"   → int  (keine Dezimalstellen)
        //         "8.2"   → decimal (hat Dezimalstellen)
        //         "8"     → int  (kein Punkt → ganzzahlig)
        //         null    → int  (keine Info → sicherste Annahme)
        if (string.IsNullOrWhiteSpace(format))
            return new("int", null, true);

        var dot = format.IndexOf('.');
        if (dot < 0)
            return new("int", null, true);

        var decimals = format[(dot + 1)..].Trim();
        return decimals is "" or "0"
            ? new("int", null, true)
            : new("decimal", null, true);
    }

    public static string DefaultValue(string clrType) => clrType switch
    {
        "string"  => "\"\"",
        "bool"    => "false",
        "int"     => "0",
        "long"    => "0",
        "decimal" => "0m",
        "DateTime" => "default",
        _ => "default",
    };
}

internal record MappedType(
    string ClrType,
    OpaccDataType? DataTypeAnnotation,
    bool IsValueType
);
