using System.Globalization;
using Opacc.Client.Enums;

namespace Opacc.Client.Helper;

/// <summary>
/// Converts CLR values to the string format used in Opacc SaveBo field assignments.
/// The caller is responsible for prepending '@' to the result:  FieldName=@{Serialize(value)}
/// </summary>
internal static class OpaccValueSerializer
{
    public static string Serialize(object? value, OpaccDataType dataType = OpaccDataType.None)
    {
        if (value == null) return "";

        return value switch
        {
            bool b => b ? "1" : "0",

            DateTime dt when dataType is OpaccDataType.Date
                => dt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),

            DateTime dt
                => dt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),

            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double  d => d.ToString(CultureInfo.InvariantCulture),
            float   f => f.ToString(CultureInfo.InvariantCulture),

            _ => value.ToString() ?? "",
        };
    }
}
