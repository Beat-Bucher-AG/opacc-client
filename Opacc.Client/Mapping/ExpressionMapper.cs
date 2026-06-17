using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Metadata;

namespace Opacc.Client.Mapping;

internal static class ProjectionMapper
{
    /// <summary>
    /// Findet für jedes Property in TResult das passende Property in TSource
    /// basierend auf dem Namen. Gibt die CLR-Namen der TSource-Properties zurück.
    /// </summary>
    public static List<string> ResolvePropertyNames<TSource, TResult>(EntityMetadata sourceMetadata)
    {
        var resultProperties = typeof(TResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var names = new List<string>();

        foreach (var resultProp in resultProperties)
        {
            if (sourceMetadata.Properties.ContainsKey(resultProp.Name))
                names.Add(resultProp.Name);
        }

        return names;
    }
}
