using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Metadata;

namespace Opacc.Client.Helper;

internal static class FilterTranslator
{
    /// <summary>
    /// Ersetzt {PropertyName} Tags in einem Filter-String durch die
    /// Opacc-Expressions aus den Metadata.
    ///
    /// Beispiel:
    ///   Input:  "{City} = 'Zürich' AND {IsPassive} = 0"
    ///   Output: "Addr.City = 'Zürich' AND Addr.IsPassive = 0"
    /// </summary>
    public static string Translate(string filter, EntityMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return filter;

        var result = filter;

        // Alle {Tags} finden und ersetzen
        var pos = 0;
        while (pos < result.Length)
        {
            var start = result.IndexOf('{', pos);
            if (start < 0)
                break;

            var end = result.IndexOf('}', start + 1);
            if (end < 0)
                break;

            var tagName = result[(start + 1)..end].Trim();

            if (metadata.Properties.TryGetValue(tagName, out var propMeta))
            {
                var replacement = propMeta.OoExpression;
                if (!replacement.Contains('.'))
                    replacement = metadata.BoEntity + "." + replacement;

                result = result[..start] + replacement + result[(end + 1)..];
                pos = start + replacement.Length;
            }
            else
            {
                // Tag nicht gefunden — unverändert lassen
                pos = end + 1;
            }
        }

        return result;
    }
}
